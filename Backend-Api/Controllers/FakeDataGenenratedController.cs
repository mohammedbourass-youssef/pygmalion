using System.Diagnostics;
using Backend_Api.Constants;
using Backend_Api.Dtos;
using Backend_Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Api.Controllers;

[ApiController]
[Route("api/v1/Pygmalion")]
public sealed class FakeDateGenenratedController(
    IDateGenerationService dateGenerationService,
    CodeParsingService codeParsingService,
    IConfiguration configuration,
    IFakeDataGenerator fakeDataGenerator,
    ILogger<FakeDateGenenratedController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Generate() => Ok(dateGenerationService.Generate());

    [HttpGet("languages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSupportedLanguages() => Ok(LanguagesQueries.Queries.Keys);

    [HttpPost("create")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateFakeData([FromBody] FakeDataRequestDto request)
    {
        var totalTimer = Stopwatch.StartNew();
        var language = request.Language.Trim().ToUpperInvariant();
        var requestedRootCount = request.NumberOfRoots;
        var requestedNestedCount = request.CountNested;
        var rootCount = GenerationLimits.ClampRootCount(requestedRootCount);
        var nestedCount = GenerationLimits.ClampNestedCount(requestedNestedCount);
        using var activity = PygmalionTelemetry.ActivitySource.StartActivity("pygmalion.generate");
        activity?.SetTag("pygmalion.language", language);
        activity?.SetTag("pygmalion.requested_root_count", requestedRootCount);
        activity?.SetTag("pygmalion.requested_nested_count", requestedNestedCount);
        activity?.SetTag("pygmalion.root_count", rootCount);
        activity?.SetTag("pygmalion.nested_count", nestedCount);
        PygmalionTelemetry.RecordRequestedSizes(language, requestedRootCount, requestedNestedCount, rootCount, nestedCount);
        if (requestedRootCount != rootCount || requestedNestedCount != nestedCount)
        {
            logger.LogInformation(
                "Applied public generation caps. Language={Language} RequestedRootCount={RequestedRootCount} EffectiveRootCount={EffectiveRootCount} RequestedNestedCount={RequestedNestedCount} EffectiveNestedCount={EffectiveNestedCount}",
                language,
                requestedRootCount,
                rootCount,
                requestedNestedCount,
                nestedCount);
        }

        try
        {
            var parseTimer = Stopwatch.StartNew();
            Dictionary<int, ExtractedClass> classes;
            using (PygmalionTelemetry.ActivitySource.StartActivity("pygmalion.parse"))
                classes = codeParsingService.ExtractClasses(request.Language, request.Code);
            parseTimer.Stop();
            PygmalionTelemetry.RecordPhase(language, "parse", parseTimer.Elapsed.TotalMilliseconds);

            logger.LogInformation(
                "Parsed generation schema. Language={Language} Classes={ClassCount} ParseDurationMs={ParseDurationMs}",
                language,
                classes.Count,
                parseTimer.Elapsed.TotalMilliseconds);

            if (classes.Count == 0)
            {
                PygmalionTelemetry.RecordFailure(language, "parse");
                return BadRequest(new
                {
                    message = "No classes with fields were found. Check that the selected language matches the source code and that the class contains fields or properties."
                });
            }

            if (!fakeDataGenerator.ValidateAllClassesProvided(request.Language, classes, out var missingClasses))
            {
                PygmalionTelemetry.RecordFailure(language, "validation");
                logger.LogWarning(
                    "Generation rejected due to missing nested classes. Language={Language} MissingClasses={MissingClasses}",
                    language,
                    missingClasses);
                return BadRequest(new { message = "Some referenced classes were not provided.", missingClasses });
            }

            var outputDirectory = configuration["GeneratedData:OutputDirectory"] ?? "output";
            var generationTimer = Stopwatch.StartNew();
            Guid fileId;
            using (PygmalionTelemetry.ActivitySource.StartActivity("pygmalion.file.generate"))
            {
                fileId = await fakeDataGenerator.GenerateAndSaveJsonAsync(
                    classes,
                    rootCount,
                    nestedCount,
                    outputDirectory);
            }
            generationTimer.Stop();
            PygmalionTelemetry.RecordPhase(language, "fake_data_and_file_write", generationTimer.Elapsed.TotalMilliseconds);
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), outputDirectory, $"{fileId:D}.json");
            PygmalionTelemetry.RecordOutputBytes(language, new FileInfo(filePath).Length);
            totalTimer.Stop();
            PygmalionTelemetry.RecordPhase(language, "total", totalTimer.Elapsed.TotalMilliseconds);
            PygmalionTelemetry.RecordRequest(language);
            activity?.SetTag("pygmalion.file_id", fileId);
            logger.LogInformation(
                "Generation completed. Language={Language} FileId={FileId} RequestedRootCount={RequestedRootCount} RootCount={RootCount} RequestedNestedCount={RequestedNestedCount} NestedCount={NestedCount} GenerationDurationMs={GenerationDurationMs} TotalDurationMs={TotalDurationMs}",
                language,
                fileId,
                requestedRootCount,
                rootCount,
                requestedNestedCount,
                nestedCount,
                generationTimer.Elapsed.TotalMilliseconds,
                totalTimer.Elapsed.TotalMilliseconds);
            string schema = configuration["UseSsl"] == "true" ? "https" : "http";
            string fileInternetPath = $"{schema}://{configuration["DomaineName"]}/api/v1/Pygmalion/{fileId}";
            return Ok(fileInternetPath);
        }
        catch (ArgumentException ex)
        {
            PygmalionTelemetry.RecordFailure(language, "request");
            logger.LogWarning(ex, "Invalid generation request. Language={Language}", language);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            PygmalionTelemetry.RecordFailure(language, "exception");
            logger.LogError(ex, "Generation failed. Language={Language}", language);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

}
