using AuthService.Api.Startup;
using AuthService.Application.Authentication.Login;

namespace AuthService.Api.Endpoints.Authentication;

public static class LoginUserEndpoint
{
    public static void MapLoginUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
                HttpContext context,
                LoginUserRequest request,
                LoginUserHandler handler,
                ILogger<Program> logger) =>
            {
                try
                {
                    LoginUserResult result = await handler.Handle(request, context.RequestAborted);
                    return result.ToHttp(context);
                }
                catch (OperationCanceledException)
                {
                    logger.LogDebug("login cancelled by client");
                    return Results.Problem("request cancelled", statusCode: 400);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error during login for {email}", request.Email);
                    return Results.Problem("login failed", statusCode: 500);
                }
            })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
            .RequireGlobalIpRateLimiting();
    }
}