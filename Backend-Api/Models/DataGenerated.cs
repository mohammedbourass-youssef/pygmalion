namespace Backend_Api.Models;

/// <summary>Catalog entry for a generated JSON payload and the inputs that produced it.</summary>
public sealed class DataGenerated
{
    public Guid Id { get; set; }
    public string Metadata { get; set; } = "{}";
    public DateTimeOffset DateCreated { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
}
