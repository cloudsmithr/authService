using System.Security.Claims;

namespace AuthService.Infrastructure.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        string? id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? user.FindFirst("sub")?.Value;

        if (Guid.TryParse(id, out Guid guid))
        {
            return guid;
        }

        return null;
    }
}