using Backend_Api.Data;
using Backend_Api.Dtos;
using Backend_Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        var service = new GeneratedDataService(dbContext, new FakeDataGenerator(), configuration);
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
}
