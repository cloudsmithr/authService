using AuthService.Application.Interfaces;
using AuthService.Application.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthService.Infrastructure.Extensions;
using AuthService.Infrastructure.Interfaces;

namespace AuthService.Application.Authentication.ResetPassword;

public class ResetPasswordHandler
{
    private readonly IResetPasswordService _resetPasswordService;
    private readonly ResetPasswordSettings _resetPasswordSettings;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        IResetPasswordService passwordResetService,
        IOptions<ResetPasswordSettings> settings,
        ILogger<ResetPasswordHandler> logger)
    {
        _resetPasswordService = passwordResetService;
        _resetPasswordSettings = settings.Value;
        _logger = logger;
    }
    
    public async Task<ResetPasswordResult> Handle(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        // We're passing this to the API Utils to enforce that the result isn't returned faster than the minimum duration in the settings.
        // This is just to ensure that impactful API calls "feel" impactful even on very fast systems.
        return await APIUtils.HandleWithMinimumTime(
            () => ResetPasswordAsync(request, cancellationToken),
            _resetPasswordSettings.MinimumDurationMs
        );
    }

    private async Task<ResetPasswordResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return new ResetPasswordResult(ResetPasswordOutcome.BadRequest, "request can't be null");

        if (string.IsNullOrWhiteSpace(request.VerificationToken))
            return new ResetPasswordResult(ResetPasswordOutcome.BadRequest, "VerificationToken can't be empty");
        
        if (string.IsNullOrWhiteSpace(request.Password))
            return new ResetPasswordResult(ResetPasswordOutcome.BadRequest, "Password can't be empty");

        try
        {
            ResetPasswordOutcome outcome = await _resetPasswordService.ResetPassword(request.VerificationToken, request.Password, cancellationToken);
            
            return new ResetPasswordResult(outcome);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Resetting of password via token {token} cancelled", request.VerificationToken.RedactToken());
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resetting of password via token {token} failed.", request.VerificationToken.RedactToken());
            throw;
        }
    }
}