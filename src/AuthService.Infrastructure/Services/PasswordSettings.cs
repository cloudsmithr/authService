namespace AuthService.Infrastructure.Services;

public class PasswordSettings
{
    public required int Iterations { get; set; }
    public required int MemorySizeKb { get; set; }
    public required int DegreeOfParallelism { get; set; }
    public required int HashLength { get; set; }
    public required int SaltLength { get; set; }    
}