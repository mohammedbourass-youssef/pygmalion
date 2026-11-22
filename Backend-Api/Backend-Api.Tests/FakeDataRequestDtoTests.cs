using System.ComponentModel.DataAnnotations;
using Backend_Api.Dtos;
using Xunit;

namespace Backend_Api.Tests;

public sealed class FakeDataRequestDtoTests
{
    [Fact]
    public void RequestBodyExample_RejectsEmptyLanguageAndCode()
    {
        var request = new FakeDataRequestDto
        {
            Language = "",
            Code = "",
            NumberOfRoots = 1,
            CountNested = 1
        };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Equal(2, validationResults.Count);
    }
}
