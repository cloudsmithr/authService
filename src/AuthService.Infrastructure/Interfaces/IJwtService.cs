using AuthService.Infrastructure.Services;

namespace AuthService.Infrastructure.Interfaces;

public interface IJwtService
{
    ApiToken GenerateToken(Guid userId, string email);
}