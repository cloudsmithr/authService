using AuthService.Domain.Entities;
using AuthService.Infrastructure.Services;

namespace AuthService.Infrastructure.Interfaces;

public interface IRefreshTokenService
{
    Task<ApiToken> GenerateRefreshToken(
        User user,
        RefreshToken? oldToken = null,
        bool purgeOldTokens = false,
        CancellationToken cancellationToken = default);

    public Task<bool> IsRefreshTokenValid(string token, CancellationToken cancellationToken);

    public Task<RefreshToken?> GetValidRefreshToken(string token, CancellationToken cancellationToken);

    public Task PurgeAllTokens(Guid userId, CancellationToken cancellationToken);
}