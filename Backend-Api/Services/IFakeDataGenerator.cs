using Backend_Api.Dtos;

namespace Backend_Api.Services;

public interface IFakeDataGenerator
{
    bool ValidateAllClassesProvided(string languageName, Dictionary<int, ExtractedClass> classes, out List<string> missingClasses);
    ExtractedClass FindRootClass(Dictionary<int, ExtractedClass> classes);
    List<Dictionary<string, object?>> GenerateFakeData(Dictionary<int, ExtractedClass> classes, int count = 5, int nestedCount = 3, string? explicitRootClassName = null);
    Task<Guid> GenerateAndSaveJsonAsync(Dictionary<int, ExtractedClass> classes, int count = 5, int nestedCount = 3, string outputDirectory = "output", string? explicitRootClassName = null);
}
