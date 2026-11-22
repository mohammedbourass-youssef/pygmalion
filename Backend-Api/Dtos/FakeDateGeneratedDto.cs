using System.ComponentModel.DataAnnotations;

namespace Backend_Api.Dtos;

public sealed record FakeDateGeneratedDto(DateTime Date);

public record Sample(string Title, string Code);
public record ExtractedField(string Name, string? Type);
public record ExtractedClass(string Name, List<ExtractedField> Fields);

public class FakeDataRequestDto
{
    [Required]
    public string Language { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    [Range(1, 10000)]
    public int NumberOfRoots { get; set; } = 10;

    [Range(1, 10000)]
    public int CountNested { get; set; } = 40;
}
