using AuthService.Application.Interfaces;
using AuthService.Application.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Services;

namespace AuthService.Application.Authentication.VerifyEmail;

public class VerifyEmailHandler
{
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly VerifyEmailSettings _verifyEmailSettings;
    private readonly ILogger<VerifyEmailHandler> _logger;

    public VerifyEmailHandler(
        IEmailVerificationService emailVerificationService,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        IOptions<VerifyEmailSettings> settings,
        ILogger<VerifyEmailHandler> logger)
    {
        _emailVerificationService = emailVerificationService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _verifyEmailSettings = settings.Value;
        _logger = logger;
    }
    
    public async Task<VerifyEmailResult> Handle(VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        // We're passing this to the API Utils to enforce that the result isn't returned faster than the minimum duration in the settings.
        // This is just to ensure that impactful API calls "feel" impactful even on very fast systems.
        return await APIUtils.HandleWithMinimumTime(
            () => VerifyEmailAsync(request, cancellationToken),
            _verifyEmailSettings.MinimumDurationMs
        );
    }

    private async Task<VerifyEmailResult> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return new VerifyEmailResult(VerifyEmailOutcome.BadRequest, message:"request can't be null");
        }

        if (string.IsNullOrWhiteSpace(request.VerificationToken))
        {
            return new VerifyEmailResult(VerifyEmailOutcome.BadRequest, message:"email can't be empty");
        }
        
        try
        {
            // let's verify the email and return the outcome we get. 
            (VerifyEmailOutcome outcome, User? user) = await _emailVerificationService.VerifyEmail(request.VerificationToken, cancellationToken);
            
            if (outcome == VerifyEmailOutcome.Success && user != null)
            {
                // Generate tokens from user
                ApiToken accessToken = _jwtService.GenerateToken(user.Id, user.Email);
                ApiToken refreshToken = await _refreshTokenService.GenerateRefreshToken(
                    user,
                    purgeOldTokens: true,
                    cancellationToken: cancellationToken);
                
                return new VerifyEmailResult(outcome, accessToken: accessToken, refreshToken: refreshToken);
            }
            
            return new VerifyEmailResult(outcome);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Verification of token {token} cancelled", request.VerificationToken);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verification of token {token} failed.", request.VerificationToken);
            throw;
        }
    }
}