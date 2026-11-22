namespace Backend_Api.Services;

public interface IGeneratedDataFileService
{
    Task<string?> GetJsonAsync(Guid id, CancellationToken cancellationToken = default);
}
