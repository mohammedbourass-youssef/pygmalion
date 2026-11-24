using Backend_Api.Constants;
using Backend_Api.Data;
using Backend_Api.Dtos;
using Backend_Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Backend_Api.Tests;

public sealed class GeneratedDataServiceTests
{
    [Fact]
    public async Task GenerateOrReuseAsync_ReusesSameFileForIdenticalInputs()
    {
        var options = new DbContextOptionsBuilder<PygmalionDbContext>()
            .UseInMemoryDatabase($"generated-data-{Guid.NewGuid():N}")
            .Options;
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"pygmalion-generated-{Guid.NewGuid():N}");
        await using var dbContext = new PygmalionDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GeneratedData:OutputDirectory"] = outputDirectory
            })
            .Build();
        var service = new GeneratedDataService(
            dbContext,
            new FakeDataGenerator(),
            configuration,
            NullLogger<GeneratedDataService>.Instance);
        var classes = new Dictionary<int, ExtractedClass>
        {
            [0] = new("Person", new List<ExtractedField> { new("Name", "string") })
        };

        try
        {
            var first = await service.GenerateOrReuseAsync("JAVA", "class Person { String Name; }", classes, 5, 3);
            var repeated = await service.GenerateOrReuseAsync("JAVA", "class Person { String Name; }", classes, 5, 3);
            var differentCount = await service.GenerateOrReuseAsync("JAVA", "class Person { String Name; }", classes, 10, 3);

            Assert.Equal(first.Id, repeated.Id);
            Assert.Equal(first.FilePath, repeated.FilePath);
            Assert.NotEqual(first.Id, differentCount.Id);
            Assert.True(File.Exists(first.FilePath));
            Assert.True(File.Exists(differentCount.FilePath));
            Assert.Equal(2, await dbContext.DataGenerated.CountAsync());
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateOrReuseAsync_WritesPublicCapPayloadAsValidJson()
    {
        var options = new DbContextOptionsBuilder<PygmalionDbContext>()
            .UseInMemoryDatabase($"generated-data-load-{Guid.NewGuid():N}")
            .Options;
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"pygmalion-load-{Guid.NewGuid():N}");
        await using var dbContext = new PygmalionDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GeneratedData:OutputDirectory"] = outputDirectory
            })
            .Build();
        var service = new GeneratedDataService(
            dbContext,
            new FakeDataGenerator(),
            configuration,
            NullLogger<GeneratedDataService>.Instance);
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
            var generated = await service.GenerateOrReuseAsync(
                "JAVA",
                "class Person { List<Order> Orders; } class Order { int Id; decimal Total; }",
                classes,
                count: GenerationLimits.MaxRootCount,
                nestedCount: GenerationLimits.MaxNestedCount);

            await using var file = File.OpenRead(generated.FilePath);
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
