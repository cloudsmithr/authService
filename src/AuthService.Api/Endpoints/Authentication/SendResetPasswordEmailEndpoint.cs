using AuthService.Api.Startup;
using AuthService.Application.Authentication.ResendEmailVerification;
using AuthService.Application.Authentication.SendResetPasswordEmail;

namespace AuthService.Api.Endpoints.Authentication;

public static class SendResetPasswordEmailEndpoint
{
    public static void MapSendResetPasswordEmailEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/sendPasswordResetEmail", async (
                HttpContext context,
                SendResetPasswordEmailRequest request,
                SendResetPasswordEmailHandler handler,
                ILogger<Program> logger) =>
            {
                try
                {
                    SendResetPasswordEmailResult result = await handler.Handle(request, context.RequestAborted);
                    return result.ToHttp();
                }
                catch (OperationCanceledException)
                {
                    logger.LogDebug("Sending password reset email cancelled by client");
                    return Results.Problem("request cancelled", statusCode: 400);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error during sending of password reset for user {user}", request.Email);
                    return Results.Problem("Sending password reset email failed. Please try again later.", statusCode: 500);
                }
            })
            .RequireGlobalIpRateLimiting();
    }    
}