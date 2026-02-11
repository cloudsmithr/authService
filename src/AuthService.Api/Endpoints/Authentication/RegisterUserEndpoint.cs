using AuthService.Api.Startup;
using AuthService.Application.Authentication.Register;

namespace AuthService.Api.Endpoints.Authentication;

public static class RegisterUserEndpoint
{
    public static void MapRegisterUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", async (
                HttpContext context,
                RegisterUserRequest request,
                RegisterUserHandler handler,
                ILogger<Program> logger) =>
            {
                try
                {
                    RegisterUserResult result = await handler.Handle(request, context.RequestAborted);
                    return result.ToHttp();
                }
                catch (OperationCanceledException)
                {
                    logger.LogDebug("registration cancelled by client");
                    return Results.Problem("request cancelled", statusCode: 400);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error during registration for user {user}", request.Email);
                    return Results.Problem("Register user failed.", statusCode: 500);
                }
            })
            .RequireGlobalIpRateLimiting();
    }
}