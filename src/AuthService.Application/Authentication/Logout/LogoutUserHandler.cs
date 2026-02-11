using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AuthService.Infrastructure.Extensions;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Application.Authentication.Logout;

public class LogoutUserHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<LogoutUserHandler> _logger;
    
    public LogoutUserHandler(
        AppDbContext db,
        ILogger<LogoutUserHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LogoutUserResult> Handle(
        LogoutUserRequest request,
        Guid userIdGuid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshTokenToRevoke))
        {
            _logger.LogInformation("Empty RefreshTokenToRevoke");
            return new LogoutUserResult(LogoutUserOutcome.BadRequest, "RefreshTokenToRevoke cannot be empty.");
        }
        
        AuthService.Domain.Entities.RefreshToken? storedRefreshToken = await _db.RefreshTokens.FirstOrDefaultAsync(t =>
            t.Token == request.RefreshTokenToRevoke &&
            t.UserId == userIdGuid,
            cancellationToken);

        // We don't have this token stored
        if (storedRefreshToken == null)
        {
            _logger.LogInformation("Refresh token {token} not found", request.RefreshTokenToRevoke.RedactToken());
            return new LogoutUserResult(LogoutUserOutcome.NotFound, "token not found");
        }
        
        // The token is already revoked, in which case we still want to successfully logout the user.
        if (storedRefreshToken.RevokedAtUtc != null)
        {
            _logger.LogDebug("Refresh token {token} already revoked", request.RefreshTokenToRevoke.RedactToken());
            return new LogoutUserResult(LogoutUserOutcome.AlreadyRevoked, "token already revoked");
        }
        
        // The token is already expired, in which case we still want to successfully logout the user.
        if (storedRefreshToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            _logger.LogDebug("refresh token {token} is expired", request.RefreshTokenToRevoke.RedactToken());
            return new LogoutUserResult(LogoutUserOutcome.Expired, "token expired");
        }
        
        storedRefreshToken.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new LogoutUserResult(LogoutUserOutcome.SuccessfullyRevoked, "logout successful");
    }
}