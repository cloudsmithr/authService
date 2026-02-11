using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Extensions;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Services;

namespace AuthService.Application.Authentication.RefreshToken;

public class RefreshTokenHandler
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<RefreshTokenHandler> _logger;
    
    public RefreshTokenHandler(
        AppDbContext db,
        IJwtService jwtService,
        ILogger<RefreshTokenHandler> logger,
        IRefreshTokenService refreshTokenService)
    {
        _db = db;
        _jwtService = jwtService;
        _logger = logger;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<RefreshTokenResult> Handle(RefreshTokenRequest request, Guid userIdGuid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            _logger.LogInformation("Refresh token is empty");
            return new RefreshTokenResult(RefreshTokenOutcome.BadRequest, message: "Refresh token is required.");
        }
        if (userIdGuid == Guid.Empty)
        {
            _logger.LogWarning("UserId is empty");
            return new RefreshTokenResult(RefreshTokenOutcome.BadRequest, message: "UserId is required.");
        }
       
        // Grab the refreshToken, make sure we're only grabbing tokens from our authenticated user.
        AuthService.Domain.Entities.RefreshToken? storedRefreshToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => 
                t.Token == request.RefreshToken &&
                t.UserId == userIdGuid,
                cancellationToken);

        // We don't have this token stored
        if (storedRefreshToken == null)
        {
            _logger.LogInformation("Did not find refresh token {token} during refresh token.", request.RefreshToken.RedactToken());
            return new RefreshTokenResult(RefreshTokenOutcome.TokenNotFound);
        }
        
        // we want to log if we're getting a refreshtoken that's already expired or revoked.
        if (storedRefreshToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            _logger.LogInformation("Submitted refreshToken {token} already expired.", request.RefreshToken.RedactToken());
            return new RefreshTokenResult(RefreshTokenOutcome.TokenExpired);
        }
        if (storedRefreshToken.RevokedAtUtc is not null)
        {
            _logger.LogInformation("Submitted refreshToken {token} already revoked.", request.RefreshToken.RedactToken());
            return new RefreshTokenResult(RefreshTokenOutcome.TokenRevoked);
        }
        
        User? user = await _db.Users.FindAsync([storedRefreshToken.UserId], cancellationToken);
        
        // We have no Users of this ID
        if (user == null)
        {
            _logger.LogError(
                "User not found but refresh token {tokenId} exists - possible data inconsistency",
                storedRefreshToken.Id);
            return new RefreshTokenResult(RefreshTokenOutcome.UserNotFound);
        }

        // let's generate a new JWT!
        ApiToken newAccessToken = _jwtService.GenerateToken(user.Id, user.Email);

        // If our stored RefreshToken isn't going to expire soon
        if (storedRefreshToken.ExpiresAtUtc >= DateTime.UtcNow.AddDays(1))
        {
            // we can just return the same refresh token
            return new RefreshTokenResult(RefreshTokenOutcome.Success, accessToken: newAccessToken, refreshToken: new ApiToken(storedRefreshToken.Token, storedRefreshToken.ExpiresAtUtc));
        }

        // we have to create a new RefreshToken. While we're doing this we'll purge any old tokens that might be floating around.
        try
        {
            ApiToken newRefreshToken = await _refreshTokenService.GenerateRefreshToken(user, storedRefreshToken, purgeOldTokens: true, cancellationToken);
            return new RefreshTokenResult(RefreshTokenOutcome.Success, accessToken: newAccessToken, refreshToken: newRefreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error happened trying to refresh the token {tokenId}", storedRefreshToken.Id);
            throw;
        }
    }
}