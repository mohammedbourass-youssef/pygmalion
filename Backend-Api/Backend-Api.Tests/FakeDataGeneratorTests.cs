using System.Text.Json;
using Backend_Api.Dtos;
using Backend_Api.Services;
using Xunit;

namespace Backend_Api.Tests;

public sealed class FakeDataGeneratorTests
{
    [Fact]
    public void GenerateFakeData_CreatesRequestedRootCountAndNestedCollectionValues()
    {
        var generator = new FakeDataGenerator();
        var classes = new Dictionary<int, ExtractedClass>
        {
            [0] = new("Person", new List<ExtractedField>
            {
                new("Id", "int"),
                new("Name", "string"),
                new("Addresses", "List<Address>")
            }),
            [1] = new("Address", new List<ExtractedField>
            {
                new("City", "string"),
                new("PostalCode", "string")
            })
        };

        var records = generator.GenerateFakeData(classes, count: 4, nestedCount: 3);

        Assert.Equal(4, records.Count);
        Assert.All(records, record =>
        {
            Assert.Contains("Id", record.Keys);
            var addresses = Assert.IsType<List<object?>>(record["Addresses"]);
            Assert.Equal(3, addresses.Count);
            Assert.All(addresses, address => Assert.IsType<Dictionary<string, object?>>(address));
        });
    }

    [Fact]
    public void ValidateAndGenerateData_SupportsPythonStyleGenericCollections()
    {
        var generator = new FakeDataGenerator();
        var classes = new Dictionary<int, ExtractedClass>
        {
            [0] = new("Person", new List<ExtractedField>
            {
                new("Addresses", "List[Address]")
            }),
            [1] = new("Address", new List<ExtractedField>
            {
                new("City", "str")
            })
        };

        Assert.True(generator.ValidateAllClassesProvided("PYTHON", classes, out var missingClasses));
        Assert.Empty(missingClasses);

        var record = Assert.Single(generator.GenerateFakeData(classes, count: 1, nestedCount: 2));
        var addresses = Assert.IsType<List<object?>>(record["Addresses"]);
        Assert.Equal(2, addresses.Count);
        Assert.All(addresses, address => Assert.IsType<Dictionary<string, object?>>(address));
    }

    [Fact]
    public void GenerateFakeData_HandlesMultipleFieldTypes()
    {
        var generator = new FakeDataGenerator();
        var classes = new Dictionary<int, ExtractedClass>
        {
            [0] = new("Sample", new List<ExtractedField>
            {
                new("Enabled", "bool"),
                new("Price", "decimal"),
                new("CreatedAt", "DateTime"),
                new("Tags", "List<string>"),
                new("Metadata", "Dictionary<string, int>")
            })
        };

        var record = Assert.Single(generator.GenerateFakeData(classes, count: 1, nestedCount: 2));

        Assert.IsType<bool>(record["Enabled"]);
        Assert.IsType<decimal>(record["Price"]);
        Assert.IsType<string>(record["CreatedAt"]);
        Assert.Equal(2, Assert.IsType<List<object?>>(record["Tags"]).Count);
        Assert.Equal(2, Assert.IsType<Dictionary<string, object?>>(record["Metadata"]).Count);
    }

    [Fact]
    public async Task GenerateAndSaveJsonAsync_WritesPrettyJsonAndReturnsFileId()
    {
        var generator = new FakeDataGenerator();
        var classes = new Dictionary<int, ExtractedClass>
        {
            [0] = new("Person", new List<ExtractedField> { new("Name", "string") })
        };
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"fake-data-tests-{Guid.NewGuid():N}");

        try
        {
            var fileId = await generator.GenerateAndSaveJsonAsync(
                classes,
                count: 2,
                nestedCount: 1,
                outputDirectory: outputDirectory);
            var filePath = Path.Combine(outputDirectory, $"{fileId}.json");

            Assert.True(File.Exists(filePath));
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(filePath));
            Assert.Equal(2, json.RootElement.GetArrayLength());
            Assert.Contains(Environment.NewLine, await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
