using AuthService.Application.Interfaces;
using AuthService.Application.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Utilities;

namespace AuthService.Application.Authentication.SendResetPasswordEmail;

public class SendResetPasswordEmailHandler
{
    private readonly AppDbContext _db;
    private readonly IResetPasswordService _passwordResetService;
    private readonly SendResetPasswordEmailSettings _sendResetPasswordEmailSettings;
    private readonly ILogger<SendResetPasswordEmailHandler> _logger;
    
    public SendResetPasswordEmailHandler(
        AppDbContext db,
        IResetPasswordService passwordResetService,
        IOptions<SendResetPasswordEmailSettings> settings,
        ILogger<SendResetPasswordEmailHandler> logger)
    {
        _db = db;
        _passwordResetService = passwordResetService;
        _sendResetPasswordEmailSettings = settings.Value;
        _logger = logger;
    }

    public async Task<SendResetPasswordEmailResult> Handle(SendResetPasswordEmailRequest request, CancellationToken cancellationToken = default)
    {
        // We're passing this to the API Utils to enforce that the result isn't returned faster than the minimum duration in the settings.
        // This is just to ensure that impactful API calls "feel" impactful even on very fast systems.
        return await APIUtils.HandleWithMinimumTime(
            () => SendResetPasswordEmailAsync(request, cancellationToken),
            _sendResetPasswordEmailSettings.MinimumDurationMs
        );
    }

    private async Task<SendResetPasswordEmailResult> SendResetPasswordEmailAsync(
        SendResetPasswordEmailRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return new SendResetPasswordEmailResult(SendResetPasswordEmailOutcome.BadRequest, "request can't be null");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new SendResetPasswordEmailResult(SendResetPasswordEmailOutcome.BadRequest, "email can't be empty");
        }

        if (!EmailUtils.IsValidEmail(request.Email))
        {
            return new SendResetPasswordEmailResult(SendResetPasswordEmailOutcome.BadRequest, "request email is not valid");
        }

        User? existingUser = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        
        if (existingUser is null)
        {
            _logger.LogDebug("User with email {Email} not found.", request.Email);
            return new SendResetPasswordEmailResult(SendResetPasswordEmailOutcome.EmailNotFound);
        }

        try
        {
            // if our user hasn't verified their email, let's send another verification email.
            await _passwordResetService.CreateAndSendResetPasswordEmail(existingUser, cancellationToken);
            _logger.LogDebug("Successfully sent password reset email email to user {Email}", request.Email);

            return new SendResetPasswordEmailResult(SendResetPasswordEmailOutcome.Success);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Sending of reset password email cancelled for user {Email}", request.Email);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sending of reset password email to {Email} failed.", request.Email);
            throw;
        }
    }
}