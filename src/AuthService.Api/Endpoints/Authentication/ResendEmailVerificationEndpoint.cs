using AuthService.Api.Startup;
using AuthService.Application.Authentication.ResendEmailVerification;

namespace AuthService.Api.Endpoints.Authentication;

public static class ResendEmailVerificationEndpoint
{
    public static void MapResendEmailVerificationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/resendVerificationMail", async (
                HttpContext context,
                ResendEmailVerificationRequest request,
                ResendEmailVerificationHandler handler,
                ILogger<Program> logger) =>
        {
            try
            {
                ResendEmailVerificationResult result = await handler.Handle(request, context.RequestAborted);
                return result.ToHttp();
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Resending verification email cancelled by client");
                return Results.Problem("request cancelled", statusCode: 400);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error during resending of verification email for user {user}", request.Email);
                return Results.Problem("Resending verification email failed. Please try again later.", statusCode: 500);
            }
        })
        .RequireGlobalIpRateLimiting();
    }
}