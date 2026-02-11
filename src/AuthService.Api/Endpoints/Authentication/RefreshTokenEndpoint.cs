using System.Security.Claims;
using AuthService.Api.Startup;
using AuthService.Application.Authentication.RefreshToken;
using AuthService.Infrastructure.Extensions;

namespace AuthService.Api.Endpoints.Authentication;

public static class RefreshTokenEndpoint
{
    public static void MapRefreshTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/refreshToken", async (
                HttpContext context,
                RefreshTokenRequest request,
                RefreshTokenHandler handler,
                ILogger<Program> logger) =>
            {
                try
                {
                    string? userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out Guid userIdGuid))
                    {
                        logger.LogWarning(
                            "Could not update refreshToken, user id {userId} is invalid",
                            userIdString);
                        return Results.Unauthorized();
                    }

                    RefreshTokenResult result = await handler.Handle(request, userIdGuid, context.RequestAborted);
                    return result.ToHttp(context);
                }
                catch (OperationCanceledException)
                {
                    logger.LogDebug("refresh token cancelled by client");
                    return Results.Problem("request cancelled", statusCode: 400);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error refreshing token {token}", request.RefreshToken.RedactToken());
                    return Results.Problem("Refresh token failed.", statusCode: 500);
                }
            })
            .RequireGlobalIpRateLimiting();
    }
}