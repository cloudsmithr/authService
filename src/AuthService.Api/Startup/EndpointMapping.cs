using Microsoft.EntityFrameworkCore;
using AuthService.Api.Endpoints.Authentication;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Api.Startup;

public static class EndpointMapping
{
    public static void MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        // Authentication Endpoints
        app.MapRegisterUserEndpoint();
        app.MapLoginUserEndpoint();
        app.MapRefreshTokenEndpoint();
        app.MapLogoutUserEndpoint();
        app.MapResendEmailVerificationEndpoint();
        app.MapVerifyEmailEndpoint();
        app.MapResetPasswordEndpoint();
        app.MapSendResetPasswordEmailEndpoint();
        
        // Sample endpoint
        app.MapGet("/api/ping", () => Results.Ok(new { ok = true, message = "pong" }));

        // Healthcheck endpoint for the DB
        app.MapHealthChecks("/healthz");

        // Simple DB Select endpoint
        app.MapGet("/readyz", async (AppDbContext db) =>
        {
            try { await db.Database.ExecuteSqlRawAsync("SELECT 1"); return Results.Ok(); }
            catch { return Results.StatusCode(503); }
        });
    }
}