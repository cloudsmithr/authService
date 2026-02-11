using AuthService.Api.Startup;
using AuthService.Application.Authentication.VerifyEmail;
using AuthService.Infrastructure.Extensions;

namespace AuthService.Api.Endpoints.Authentication;

public static class VerifyEmailEndpoint
{
    public static void MapVerifyEmailEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/verifyEmail", async (
                HttpContext context,
                VerifyEmailRequest request,
                VerifyEmailHandler handler,
                ILogger<Program> logger) =>
            {
                try
                {
                    VerifyEmailResult result = await handler.Handle(request, context.RequestAborted);
                    return result.ToHttp(context);
                }
                catch (OperationCanceledException)
                {
                    logger.LogDebug("Email verification cancelled by client");
                    return Results.Problem("request cancelled", statusCode: 400);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error during email verification, token was {token}", request.VerificationToken.RedactToken());
                    return Results.Problem("Email verification failed. Please try again later.", statusCode: 500);
                }
            })
            .RequireGlobalIpRateLimiting();
    }
}