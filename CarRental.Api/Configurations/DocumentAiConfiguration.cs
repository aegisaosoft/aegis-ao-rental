namespace CarRental.Api.Configurations;

/// <summary>
/// Configuration for Google Document AI service
/// </summary>
public interface IDocumentAiConfiguration
{
    string ProjectId { get; }
    string Location { get; }
    string ProcessorId { get; }
    string? CredentialsJson { get; }
    string? CredentialsFilePath { get; }
    bool EnableDetailedLogging { get; }
}

public class DocumentAiConfiguration : IDocumentAiConfiguration
{
    public string ProjectId { get; set; } = string.Empty;
    public string Location { get; set; } = "us";
    public string ProcessorId { get; set; } = string.Empty;
    public string? CredentialsJson { get; set; }
    public string? CredentialsFilePath { get; set; }
    public bool EnableDetailedLogging { get; set; } = false;
}