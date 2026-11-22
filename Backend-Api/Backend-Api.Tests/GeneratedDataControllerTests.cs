using System.Text.Json;
using Backend_Api.Controllers;
using Backend_Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using Xunit;

namespace Backend_Api.Tests;

public sealed class GeneratedDataControllerTests
{
    [Fact]
    public void GetById_UsesExpectedVersionedRoute()
    {
        var controllerRoute = typeof(GeneratedDataController).GetCustomAttribute<RouteAttribute>();
        var actionRoute = typeof(GeneratedDataController)
            .GetMethod(nameof(GeneratedDataController.GetById))!
            .GetCustomAttribute<HttpGetAttribute>();

        Assert.Equal("api/v1/Pygmalion", controllerRoute?.Template);
        Assert.Equal("{id:guid}", actionRoute?.Template);
    }

    [Fact]
    public async Task GetById_ReturnsStoredJsonAsJsonContent()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"generated-data-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var id = Guid.NewGuid();
        const string json = "[{\"name\":\"Ada\"}]";

        try
        {
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"{id:D}.json"), json);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GeneratedData:OutputDirectory"] = outputDirectory
                })
                .Build();
            var controller = new GeneratedDataController(new GeneratedDataFileService(configuration));

            var result = await controller.GetById(id, CancellationToken.None);

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal(json, contentResult.Content);
            Assert.Equal("application/json", contentResult.ContentType);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundWhenFileDoesNotExist()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"generated-data-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GeneratedData:OutputDirectory"] = outputDirectory
            })
            .Build();
        var controller = new GeneratedDataController(new GeneratedDataFileService(configuration));

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("There is no data", JsonSerializer.Serialize(notFoundResult.Value));
    }
}
