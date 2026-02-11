namespace AuthService.Infrastructure.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string email, string token);
    Task SendPasswordResetEmailAsync(string email, string token);
    Task SendWelcomeEmailAsync(string email, string userName);
    Task SendEmailAsync(string to, string subject, string htmlBody);
}