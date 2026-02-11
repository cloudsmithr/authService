using AuthService.Application.Authentication.VerifyEmail;
using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IEmailVerificationService
{
    public Task CreateAndSendEmailVerification(User user, CancellationToken cancellationToken);
    public Task<(VerifyEmailOutcome outcome, User? user)> VerifyEmail(string token, CancellationToken cancellationToken);

}