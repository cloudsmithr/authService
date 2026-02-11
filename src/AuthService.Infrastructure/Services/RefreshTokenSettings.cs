namespace AuthService.Infrastructure.Services;

public class RefreshTokenSettings
{
    public required int RefreshTokenLength { get; set; }
    public required int RefreshTokenLifeTimeInHours { get; set; }
    public required int RefreshTokenPurgeCutoffInDays { get; set; }
}