using System.Text.Json;
using Backend_Api.Constants;
using Backend_Api.Dtos;
using Backend_Api.Services;
using Xunit;

namespace Backend_Api.Tests;

public sealed class GeneratedFileTests
{
    [Fact]
    public async Task GenerateAndSaveJsonAsync_WritesUniqueFilesForIdenticalInputs()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"pygmalion-generated-{Guid.NewGuid():N}");
        var generator = new FakeDataGenerator();
        var classes = new Dictionary<int, ExtractedClass>
        {
            [0] = new("Person", new List<ExtractedField> { new("Name", "string") })
        };

        try
        {
            var first = await generator.GenerateAndSaveJsonAsync(classes, 5, 3, outputDirectory);
            var repeated = await generator.GenerateAndSaveJsonAsync(classes, 5, 3, outputDirectory);
            var differentCount = await generator.GenerateAndSaveJsonAsync(classes, 10, 3, outputDirectory);

            Assert.NotEqual(first, repeated);
            Assert.NotEqual(first, differentCount);
            Assert.True(File.Exists(Path.Combine(outputDirectory, $"{first:D}.json")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, $"{repeated:D}.json")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, $"{differentCount:D}.json")));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAndSaveJsonAsync_WritesPublicCapPayloadAsValidJson()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"pygmalion-load-{Guid.NewGuid():N}");
        var generator = new FakeDataGenerator();
        var classes = new Dictionary<int, ExtractedClass>
        {
            [0] = new("Person", new List<ExtractedField>
            {
                new("Name", "string"),
                new("Orders", "List<Order>")
            }),
            [1] = new("Order", new List<ExtractedField>
            {
                new("Id", "int"),
                new("Total", "decimal")
            })
        };

        try
        {
            var fileId = await generator.GenerateAndSaveJsonAsync(
                classes,
                GenerationLimits.MaxRootCount,
                GenerationLimits.MaxNestedCount,
                outputDirectory);
            var filePath = Path.Combine(outputDirectory, $"{fileId:D}.json");

            await using var file = File.OpenRead(filePath);
            using var json = await JsonDocument.ParseAsync(file);
            Assert.Equal(GenerationLimits.MaxRootCount, json.RootElement.GetArrayLength());
            Assert.Equal(GenerationLimits.MaxNestedCount, json.RootElement[0].GetProperty("Orders").GetArrayLength());
            Assert.True(file.Length > 0);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
