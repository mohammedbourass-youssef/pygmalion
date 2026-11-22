using Backend_Api.Services;
using Xunit;

namespace Backend_Api.Tests;

public sealed class CodeParsingServiceTests
{
    private readonly CodeParsingService _parser = new();

    [Theory]
    [InlineData("JAVA", "public class Person { private int id; private String name; }")]
    [InlineData("C#", "public class Person { public int Id { get; set; } public string Name { get; set; } }")]
    [InlineData("PYTHON", "class Person:\n    id: int\n    name: str")]
    [InlineData("C++", "class Person { int id; }; struct Address { float latitude; };")]
    [InlineData("JAVASCRIPT", "class Person { id = 1; name = 'Ada'; }")]
    [InlineData("GO", "package main\ntype Person struct { Id int; Name string }")]
    [InlineData("TYPESCRIPT", "class Person { id: number; name: string; } interface Address { city: string; }")]
    public void ExtractClasses_ParsesSupportedLanguageFields(string language, string code)
    {
        var classes = _parser.ExtractClasses(language, code);

        Assert.NotEmpty(classes);
        Assert.Contains(classes.Values, extractedClass => extractedClass.Fields.Count > 0);
    }

    [Fact]
    public void ExtractClasses_ParsesDartFields()
    {
        var classes = _parser.ExtractClasses("DART", "class Person { int id; String name; }");

        Assert.NotEmpty(classes);
        Assert.Contains(classes.Values, extractedClass => extractedClass.Fields.Count > 0);
    }

    [Fact]
    public void ExtractClasses_PreservesDartGenericTypesForValidationAndGeneration()
    {
        const string dartCode = """
            class Person {
              List<Review> reviews;
              Map<String, String> labels;
            }
            class Review {
              String comment;
            }
            """;

        var classes = _parser.ExtractClasses("DART", dartCode);
        var person = Assert.Single(classes.Values, extractedClass => extractedClass.Name == "Person");
        Assert.Contains(person.Fields, field => field.Name == "reviews" && field.Type == "List<Review>");
        Assert.Contains(person.Fields, field => field.Name == "labels" && field.Type == "Map<String, String>");

        var generator = new FakeDataGenerator();
        Assert.True(generator.ValidateAllClassesProvided("DART", classes, out var missingClasses));
        Assert.Empty(missingClasses);

        var record = Assert.Single(generator.GenerateFakeData(classes, count: 1, nestedCount: 2));
        Assert.Equal(2, Assert.IsType<List<object?>>(record["reviews"]).Count);
        Assert.Equal(2, Assert.IsType<Dictionary<string, object?>>(record["labels"]).Count);
    }

    [Fact]
    public void ExtractClasses_ThrowsForUnsupportedLanguage()
    {
        var exception = Assert.Throws<ArgumentException>(() => _parser.ExtractClasses("Unknown", "class A {}"));

        Assert.Contains("not supported", exception.Message);
    }
}
