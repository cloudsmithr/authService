namespace AuthService.Infrastructure.Services;

public class EmailSettings
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string FromName { get; set; }
    public string FromEmail { get; set; }
    public bool UseSsl { get; set; }
    public bool LoginRequired { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string BaseUrl { get; set; }
    public string AppName { get; set; }    
}