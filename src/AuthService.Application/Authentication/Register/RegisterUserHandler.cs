using AuthService.Application.Interfaces;
using AuthService.Application.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Utilities;
using Stripe;

namespace AuthService.Application.Authentication.Register;

public class RegisterUserHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<RegisterUserHandler> _logger;
    private readonly RegisterUserSettings _registerUserSettings;
    private readonly IPasswordService _passwordService;
    private readonly IEmailVerificationService _emailVerificationService;
    
    public RegisterUserHandler(
        AppDbContext db,
        ILogger<RegisterUserHandler> logger,
        IPasswordService passwordService,
        IOptions<RegisterUserSettings> registerUserOptions,
        IEmailVerificationService emailVerificationService)
    {
        _db = db;
        _logger = logger;
        _passwordService = passwordService;
        _registerUserSettings = registerUserOptions.Value;
        _emailVerificationService = emailVerificationService;
    }
    
    public async Task<RegisterUserResult> Handle(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        // We're passing this to the API Utils to enforce that the result isn't returned faster than the minimum duration in the settings.
        // This is just to ensure that impactful API calls "feel" impactful even on very fast systems.
        return await APIUtils.HandleWithMinimumTime(
            () => RegisterUserAsync(request, cancellationToken),
            _registerUserSettings.MinimumDurationMs
        );
    }
    
    private async Task<RegisterUserResult> RegisterUserAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        // Validate request
        RegisterUserResult? validationResult = ValidateRequest(request);
        if (validationResult != null)
        {
            return validationResult;
        }

        // Check if Username already exists
        User? existingUser = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (existingUser is not null)
        {
            _logger.LogInformation("Username {Username} already exists", request.Username);
            
            if (existingUser.EmailVerified)
            {
                return new RegisterUserResult(RegisterUserOutcome.EmailAlreadyExists);
            }

            // if our user hasn't verified their email, let's send another verification email.
            await _emailVerificationService.CreateAndSendEmailVerification(existingUser, cancellationToken);
            _logger.LogDebug("Successfully sent verification email to user {Email}", request.Email);
                
            return new RegisterUserResult(RegisterUserOutcome.EmailExistsButEmailNotVerified);
        }

        _logger.LogDebug("Username {Username} does not exist", request.Username);

        // Hash Password
        (string hash, string salt) = await _passwordService.HashPassword(request.Password);
        
        // Create Users in DB
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            Username = request.Username ?? "",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            PasswordHash = hash,
            PasswordSalt = salt,
            EmailVerified = false
        };

        var customerService = new CustomerService();
        
        
        try
        {
            _logger.LogDebug("Adding user {Email}", request.Email);
            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Successfully added user {Email}", request.Email);
            
            await _emailVerificationService.CreateAndSendEmailVerification(user, cancellationToken);
            _logger.LogDebug("Successfully sent verification email to user {Email}", request.Email);
            
            return new RegisterUserResult(RegisterUserOutcome.Success);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Registration cancelled for user {Email}", request.Email);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user {Email} to database", request.Email);
            throw;
        }
    }

    private RegisterUserResult? ValidateRequest(RegisterUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            _logger.LogInformation("Registration failed: Empty Email");
            return new RegisterUserResult(RegisterUserOutcome.BadRequest, "Email is required and cannot be empty.");
        }
        
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogInformation("Registration failed: Empty password");
            return new RegisterUserResult(RegisterUserOutcome.BadRequest, "Password is required and cannot be empty.");
        }

        if (!EmailUtils.IsValidEmail(request.Email))
        {
            _logger.LogInformation("Registration failed: Invalid email");
            return new RegisterUserResult(RegisterUserOutcome.BadRequest, "Invalid email address.");
        }
        
        if (request.Password.Length < _registerUserSettings.MinimumPasswordLength)
        {
            _logger.LogInformation("Registration failed: Password too short ({Length})", request.Password.Length);
            return new RegisterUserResult(RegisterUserOutcome.BadRequest, $"Password must be at least {_registerUserSettings.MinimumPasswordLength} characters long.");
        }

        return null; // Validation passed
    }

}