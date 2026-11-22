using Backend_Api.Constants;
using Backend_Api.Dtos;
using TreeSitter;

namespace Backend_Api.Services;

public sealed class CodeParsingService
{
    public Dictionary<int, ExtractedClass> ExtractClasses(string languageName, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageName);
        ArgumentNullException.ThrowIfNull(code);

        var queryEntry = LanguagesQueries.Queries.FirstOrDefault(entry =>
            string.Equals(entry.Key, languageName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (queryEntry.Key is null)
        {
            throw new ArgumentException($"Language '{languageName}' is not supported.", nameof(languageName));
        }

        using var language = string.Equals(languageName.Trim(), "DART", StringComparison.OrdinalIgnoreCase)
            ? TreeSitterLanguagePack.TreeSitterLanguagePackConverter.GetLanguage("dart")
            : new Language(languageName.Trim());
        using var parser = new Parser(language);
        using var tree = parser.Parse(code)
            ?? throw new ArgumentException("The source code could not be parsed.", nameof(code));
        using var query = new Query(language, queryEntry.Value.Query);

        var classes = new Dictionary<int, ExtractedClass>();
        foreach (var match in query.Execute(tree.RootNode).Matches)
        {
            string? className = null;
            var classStartByte = -1;
            string? fieldName = null;
            string? fieldType = null;

            foreach (var capture in match.Captures)
            {
                switch (capture.Name)
                {
                    case "class":
                        className = capture.Node.Text;
                        classStartByte = capture.Node.StartIndex;
                        break;
                    case "name":
                        fieldName = capture.Node.Text;
                        break;
                    case "type":
                        fieldType = capture.Node.Text;
                        break;
                    case "decl":
                        fieldType = ExtractDartType(capture.Node);
                        break;
                }
            }

            if (className is null || fieldName is null)
                continue;

            if (!classes.TryGetValue(classStartByte, out var extractedClass))
            {
                extractedClass = new ExtractedClass(className, new List<ExtractedField>());
                classes[classStartByte] = extractedClass;
            }

            extractedClass.Fields.Add(new ExtractedField(fieldName, fieldType));
        }

        return classes;
    }

    private static string? ExtractDartType(Node declNode)
    {
        var children = declNode.Children.ToList();

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child.Type.EndsWith("type", StringComparison.Ordinal) || child.Type == "type_identifier")
            {
                if (i + 1 < children.Count && children[i + 1].Type == "type_arguments")
                    return child.Text + children[i + 1].Text;

                return child.Text;
            }
        }

        return null;
    }
}
