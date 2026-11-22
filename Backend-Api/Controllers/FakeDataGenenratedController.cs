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
    IGeneratedDataService generatedDataService) : ControllerBase
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
        try
        {
            var classes = codeParsingService.ExtractClasses(request.Language, request.Code);
            if (classes.Count == 0)
            {
                return BadRequest(new
                {
                    message = "No classes with fields were found. Check that the selected language matches the source code and that the class contains fields or properties."
                });
            }

            if (!fakeDataGenerator.ValidateAllClassesProvided(request.Language, classes, out var missingClasses))
            {
                return BadRequest(new { message = "Some referenced classes were not provided.", missingClasses });
            }

            var generated = await generatedDataService.GenerateOrReuseAsync(
                request.Language,
                request.Code,
                classes,
                request.NumberOfRoots,
                request.CountNested,
                HttpContext.RequestAborted);
            string schema = configuration["UseSsl"] == "true" ? "https" : "http";
            string fileInternetPath = $"{schema}://{configuration["DomaineName"]}/api/v1/Pygmalion/{generated.Id}";
            return Ok(fileInternetPath);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}
