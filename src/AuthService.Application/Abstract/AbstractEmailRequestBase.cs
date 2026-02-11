using System.ComponentModel.DataAnnotations;

namespace AuthService.Application.Abstract;

public abstract class AbstractEmailRequestBase
{
    private string _email = string.Empty;
    
    [EmailAddress]
    public required string Email 
    { 
        get => _email;
        set => _email = value?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}