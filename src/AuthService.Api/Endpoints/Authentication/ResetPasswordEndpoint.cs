using AuthService.Api.Startup;
using AuthService.Application.Authentication.ResetPassword;
using AuthService.Infrastructure.Extensions;

namespace AuthService.Api.Endpoints.Authentication;

public static class ResetPasswordEndpoint
{
    public static void MapResetPasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/resetPassword", async (
                HttpContext context,
                ResetPasswordRequest request,
                ResetPasswordHandler handler,
                ILogger<Program> logger) =>
            {
                try
                {
                    ResetPasswordResult result = await handler.Handle(request, context.RequestAborted);
                    return result.ToHttp();
                }
                catch (OperationCanceledException)
                {
                    logger.LogDebug("Email verification cancelled by client");
                    return Results.Problem("request cancelled", statusCode: 400);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error during password reset, token was {token}", request.VerificationToken.RedactToken());
                    return Results.Problem("Password reset failed. Please try again later.", statusCode: 500);
                }
            })
            .RequireGlobalIpRateLimiting();
    }
}