namespace AuthService.Application;

public class GenericErrorResponse
{
    public required string Error { get; set; }
    public required string Message { get; set; }
}