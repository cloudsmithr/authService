using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Infrastructure.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _dbContext;
    private readonly RefreshTokenSettings _refreshTokenSettings;
    private readonly ILogger<RefreshTokenService> _logger;
    
    public RefreshTokenService(
        AppDbContext dbContext,
        IOptions<RefreshTokenSettings> refreshTokenSettings,
        ILogger<RefreshTokenService> logger)
    {
        _dbContext = dbContext;
        _refreshTokenSettings = refreshTokenSettings.Value;
        _logger = logger;
    }
    
    public async Task<ApiToken> GenerateRefreshToken(
        User user,
        RefreshToken? oldToken = null,
        bool purgeOldTokens = false,
        CancellationToken cancellationToken = default)
    {
        if (user is null)
            throw new ArgumentException("User cannot be null", nameof(user));
        
        if (user.Id == Guid.Empty)
            throw new ArgumentException("User id cannot be empty", nameof(user.Id));
        
        string refreshTokenString = Convert.ToBase64String(RandomNumberGenerator.GetBytes(_refreshTokenSettings.RefreshTokenLength))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        if (string.IsNullOrWhiteSpace(refreshTokenString))
            throw new InvalidOperationException("Refresh token generation failed to produce valid token");
        
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        DateTime expiresAt = DateTime.UtcNow.AddHours(_refreshTokenSettings.RefreshTokenLifeTimeInHours);
        
        RefreshToken refreshToken = new ()
        {
            Id = Guid.NewGuid(),
            Token = refreshTokenString,
            UserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAt
        };

        // Mark old token as revoked and link it to the new token (token rotation)
        if (oldToken != null)
        {
            oldToken.RevokedAtUtc = DateTime.UtcNow;
            oldToken.ReplacedByTokenId = refreshToken.Id;
        }

        _dbContext.RefreshTokens.Add(refreshToken);
        
        if (purgeOldTokens)
            await PurgeStaleRefreshTokens(user.Id, cancellationToken);
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        
        return new ApiToken(refreshTokenString, expiresAt);
    }
    
    // For simple validation checks
    public async Task<bool> IsRefreshTokenValid(string token, CancellationToken cancellationToken)
    {
        return await _dbContext.RefreshTokens
            .AsNoTracking()
            .AnyAsync(rt => 
                    rt.Token == token && 
                    rt.ExpiresAtUtc > DateTime.UtcNow &&
                    rt.RevokedAtUtc == null, 
                cancellationToken);
    }

    // For actual token refresh flow
    public async Task<RefreshToken?> GetValidRefreshToken(string token, CancellationToken cancellationToken)
    {
        return await _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(rt => 
                    rt.Token == token && 
                    rt.ExpiresAtUtc > DateTime.UtcNow &&
                    rt.RevokedAtUtc == null, 
                cancellationToken);
    }
    

    // This should always happen on password reset.
    public async Task PurgeAllTokens(Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Purging all refresh tokens for user {userId}", userId);
        await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task PurgeStaleRefreshTokens(Guid userId, CancellationToken cancellationToken)
    {
        // Let's clean up any old tokens this user has lying around that are older than the cutoff time
        DateTime cutoff = DateTime.UtcNow.AddDays(-_refreshTokenSettings.RefreshTokenPurgeCutoffInDays);
        await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        
        _logger.LogDebug("Purged stale refresh tokens for user {userId}", userId);
    }

}