using AuthService.Api.Startup;
using AuthService.Application.Authentication.Logout;
using AuthService.Infrastructure.Extensions;

namespace AuthService.Api.Endpoints.Authentication;

public static class LogoutUserEndpoint
{
    public static void MapLogoutUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/logout", async (
                HttpContext context,
                LogoutUserRequest request,
                LogoutUserHandler handler,
                ILogger<Program> logger) =>
            {
                // On Logout we always, always, always return that it was successful, even if there was an issue in the backend.
                // If the user logs out, the most important thing is that the client removes any stored tokens from localstorage.
                // If there is an issue with refreshTokens or the database or whatever, that doesn't matter to the client - the user MUST be logged out in the client,
                // and the user must NOT be given any further information to backend status.
            
                Guid? userId = context.User.GetUserId();

                if (userId is null)
                {
                    logger.LogWarning("Invalid or missing user id, still returning 204");
                }
                else
                {
                    try
                    {
                        using (logger.BeginScope(new { user_id = userId, op = "logout" }))
                        {
                            await handler.Handle(request, userId.Value, context.RequestAborted);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogDebug("Logout cancelled by the client for user {userId}, still returning 204", userId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unhandled error during logging out with token {token} from user {userId}", request.RefreshTokenToRevoke.RedactToken(), userId);
                    }
                }
                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization()
            .RequirePerUserRefreshRateLimiting();
    }
}