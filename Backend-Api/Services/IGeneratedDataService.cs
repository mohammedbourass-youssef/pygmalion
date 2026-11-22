using Backend_Api.Dtos;
using Backend_Api.Models;

namespace Backend_Api.Services;

public interface IGeneratedDataService
{
    Task<DataGenerated> GenerateOrReuseAsync(
        string language,
        string code,
        Dictionary<int, ExtractedClass> classes,
        int count,
        int nestedCount,
        CancellationToken cancellationToken = default);
}
