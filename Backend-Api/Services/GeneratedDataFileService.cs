namespace Backend_Api.Services;

public sealed class GeneratedDataFileService(IConfiguration configuration) : IGeneratedDataFileService
{
    private readonly string _outputDirectory = Path.Combine(
        Directory.GetCurrentDirectory(),
        configuration["GeneratedData:OutputDirectory"] ?? "output");

    public async Task<string?> GetJsonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_outputDirectory, $"{id:D}.json");
        if (!File.Exists(filePath))
            return null;

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }
}
