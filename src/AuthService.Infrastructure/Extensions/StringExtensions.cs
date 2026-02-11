namespace AuthService.Infrastructure.Extensions;

public static class StringExtensions
{
    public static string RedactToken(this string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "<empty>";
        }

        return token.Length <= 6 
            ? token + "…" 
            : token.Substring(0, 6) + "…";
    }
}