using System.ComponentModel.DataAnnotations;
using Backend_Api.Constants;
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

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(10, 20, 10, 20)]
    [InlineData(501, 21, GenerationLimits.MaxRootCount, GenerationLimits.MaxNestedCount)]
    [InlineData(10000, 10000, GenerationLimits.MaxRootCount, GenerationLimits.MaxNestedCount)]
    public void GenerationLimits_ClampPublicCounts(
        int requestedRootCount,
        int requestedNestedCount,
        int expectedRootCount,
        int expectedNestedCount)
    {
        Assert.Equal(expectedRootCount, GenerationLimits.ClampRootCount(requestedRootCount));
        Assert.Equal(expectedNestedCount, GenerationLimits.ClampNestedCount(requestedNestedCount));
    }

    [Fact]
    public void RequestBodyExample_AcceptsCountsAbovePublicCapsForClamping()
    {
        var request = new FakeDataRequestDto
        {
            Language = "JAVA",
            Code = "class Person { String Name; }",
            NumberOfRoots = 10000,
            CountNested = 10000
        };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.True(isValid);
    }
}
