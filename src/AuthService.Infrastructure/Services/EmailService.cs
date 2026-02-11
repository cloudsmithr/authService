using System.Net;
using System.Net.Mail;
using System.Text;
using AuthService.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    
    public EmailService(
        IOptions<EmailSettings> options,
        ILogger<EmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }
    
    
    public async Task SendVerificationEmailAsync(string email, string token)
    {
        var verificationUrl = $"{_settings.BaseUrl}/verifyEmail?&token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}";
        
        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .button {{ 
                        display: inline-block; 
                        padding: 12px 24px; 
                        background-color: #007bff; 
                        color: white; 
                        text-decoration: none; 
                        border-radius: 5px;
                        margin: 20px 0;
                    }}
                    .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee; font-size: 12px; color: #666; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <h2>Welcome to {_settings.AppName}!</h2>
                    <p>Please verify your email address to complete your registration.</p>
                    <a href='{verificationUrl}' class='button'>Verify Email Address</a>
                    <p>Or copy and paste this link into your browser:</p>
                    <p style='word-break: break-all;'>{verificationUrl}</p>
                    <p>This link will expire in 24 hours.</p>
                    <div class='footer'>
                        <p>If you didn't create an account with us, please ignore this email.</p>
                    </div>
                </div>
            </body>
            </html>";

        await SendEmailAsync(email, $"Verify your {_settings.AppName} account", htmlBody);
    }

    public async Task SendPasswordResetEmailAsync(string email, string token)
    {
        var resetUrl = $"{_settings.BaseUrl}/resetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}";

        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .button {{ 
                        display: inline-block; 
                        padding: 12px 24px; 
                        background-color: #dc3545; 
                        color: white; 
                        text-decoration: none; 
                        border-radius: 5px;
                        margin: 20px 0;
                    }}
                    .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee; font-size: 12px; color: #666; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <h2>Password Reset Request</h2>
                    <p>We received a request to reset your password for your {_settings.AppName} account.</p>
                    <a href='{resetUrl}' class='button'>Reset Password</a>
                    <p>Or copy and paste this link into your browser:</p>
                    <p style='word-break: break-all;'>{resetUrl}</p>
                    <p>This link will expire in 1 hour.</p>
                    <div class='footer'>
                        <p>If you didn't request a password reset, please ignore this email. Your password won't be changed.</p>
                    </div>
                </div>
            </body>
            </html>";

        await SendEmailAsync(email, $"Reset your {_settings.AppName} password", htmlBody);
    }

    public async Task SendWelcomeEmailAsync(string email, string userName)
    {
        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .button {{ 
                        display: inline-block; 
                        padding: 12px 24px; 
                        background-color: #28a745; 
                        color: white; 
                        text-decoration: none; 
                        border-radius: 5px;
                        margin: 20px 0;
                    }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <h2>Welcome aboard, {userName}!</h2>
                    <p>Your account has been successfully verified. You can now access all features of {_settings.AppName}.</p>
                    <a href='{_settings.BaseUrl}/profile' class='button'>Go to Profile</a>
                    <p>If you have any questions, feel free to reach out to our support team.</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(email, $"Welcome to {_settings.AppName}!", htmlBody);
    }
    
    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            using var smtpClient = CreateSmtpClient();
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            
            mailMessage.To.Add(to);
            
            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {Recipient}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}", to);
            throw;
        }
    }
    
    private SmtpClient CreateSmtpClient()
    {
        var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        // Only add credentials if username is provided (not needed for Mailhog)
        if (!string.IsNullOrEmpty(_settings.Username))
        {
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
        }

        return client;
    }
}