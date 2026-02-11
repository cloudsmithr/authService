using AuthService.Application.Authentication.ResetPassword;
using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IResetPasswordService
{
    Task CreateAndSendResetPasswordEmail(User user, CancellationToken cancellationToken);
    Task<ResetPasswordOutcome> ResetPassword(string token, string newPassword, CancellationToken cancellationToken);
}