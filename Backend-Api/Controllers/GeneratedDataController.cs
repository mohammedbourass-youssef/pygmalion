using Backend_Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Api.Controllers;

[ApiController]
[Route("api/v1/Pygmalion")]
public sealed class GeneratedDataController(IGeneratedDataFileService generatedDataFileService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var json = await generatedDataFileService.GetJsonAsync(id, cancellationToken);
        return json is null
            ? NotFound(new { message = "There is no data for this ID." })
            : Content(json, "application/json");
    }
}
