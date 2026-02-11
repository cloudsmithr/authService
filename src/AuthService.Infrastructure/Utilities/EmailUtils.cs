using System.Text.RegularExpressions;

namespace AuthService.Infrastructure.Utilities;

public static class EmailUtils
{
    private static Regex _emailRegex = new Regex(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
            return false;
            
        return _emailRegex.IsMatch(email);
    }
}