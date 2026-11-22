using System.Text.Json;
using System.Text.RegularExpressions;
using Backend_Api.Constants;
using Backend_Api.Dtos;
using Bogus;

namespace Backend_Api.Services;

public static class FakeImageProvider
{
    // 50 unique deterministic portrait avatars (square 256x256)
    private static readonly string[] Avatars = Enumerable.Range(1, 50)
        .Select(i => $"https://i.pravatar.cc/256?img={i}")
        .ToArray();

    // 100 unique deterministic high-res photos via Picsum seeds (800x600)
    private static readonly string[] Photos = Enumerable.Range(100, 100)
        .Select(i => $"https://picsum.photos/seed/mock_{i}/800/600")
        .ToArray();

    // 50 unique deterministic wide banners/covers (1200x400)
    private static readonly string[] Banners = Enumerable.Range(200, 50)
        .Select(i => $"https://picsum.photos/seed/banner_{i}/1200/400")
        .ToArray();

    public static string GetImageUrl(Faker faker, string lowerFieldName)
    {
        if (lowerFieldName.Contains("avatar") || lowerFieldName.Contains("profile") || lowerFieldName.Contains("user"))
            return faker.PickRandom(Avatars);

        if (lowerFieldName.Contains("banner") || lowerFieldName.Contains("cover") || lowerFieldName.Contains("header"))
            return faker.PickRandom(Banners);

        return faker.PickRandom(Photos);
    }
}

public sealed class FakeDataGenerator : IFakeDataGenerator
{
    private readonly Faker _faker = new();

    private static readonly Dictionary<string, HashSet<string>> BuiltInTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JAVA"] = new() { "byte", "short", "int", "long", "float", "double", "boolean", "char", "String", "Object", "void", "Integer", "Long", "Double", "Float", "Boolean", "BigDecimal", "LocalDate", "LocalDateTime", "Instant", "Date", "UUID" },
        ["C#"] = new() { "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal", "bool", "char", "string", "object", "dynamic", "void", "DateTime", "DateTimeOffset", "DateOnly", "TimeOnly", "Guid" },
        ["PYTHON"] = new() { "int", "float", "str", "bool", "bytes", "list", "dict", "set", "tuple", "Any", "None", "datetime", "date", "UUID" },
        ["C++"] = new() { "int", "long", "short", "char", "float", "double", "bool", "void", "string", "std::string", "size_t", "int32_t", "int64_t" },
        ["GO"] = new() { "int", "int8", "int16", "int32", "int64", "uint", "uint8", "uint16", "uint32", "uint64", "float32", "float64", "bool", "string", "byte", "rune", "error", "time.Time" },
        ["DART"] = new() { "int", "double", "num", "String", "bool", "dynamic", "Object", "void", "DateTime" },
        ["TYPESCRIPT"] = new() { "number", "string", "boolean", "any", "unknown", "void", "null", "undefined", "object", "bigint", "symbol", "Date" },
        ["JAVASCRIPT"] = new()
    };

    /// <summary>
    /// Validates that every custom class type referenced by a field is present in the parsed classes.
    /// </summary>
    public bool ValidateAllClassesProvided(
        string languageName,
        Dictionary<int, ExtractedClass> classes,
        out List<string> missingClasses)
    {
        missingClasses = new List<string>();
        var definedClasses = new HashSet<string>(classes.Values.Select(c => c.Name));

        BuiltInTypes.TryGetValue(languageName, out var builtIns);
        builtIns ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cls in classes.Values)
        {
            foreach (var field in cls.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.Type))
                    continue;

                var baseType = CleanTypeName(field.Type);
                if (string.IsNullOrWhiteSpace(baseType))
                    continue;

                if (!builtIns.Contains(baseType) && !definedClasses.Contains(baseType))
                {
                    if (!missingClasses.Contains(baseType))
                        missingClasses.Add(baseType);
                }
            }
        }

        return missingClasses.Count == 0;
    }

    /// <summary>
    /// Finds the root class (the class not referenced as a field type by any other class).
    /// </summary>
    public ExtractedClass FindRootClass(Dictionary<int, ExtractedClass> classes)
    {
        var allClasses = classes.Values.ToList();
        if (allClasses.Count == 0)
            throw new InvalidOperationException("No classes found to generate data for.");

        if (allClasses.Count == 1)
            return allClasses[0];

        var definedNames = new HashSet<string>(allClasses.Select(c => c.Name));
        var referencedTypes = new HashSet<string>();

        foreach (var cls in allClasses)
        {
            foreach (var field in cls.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.Type))
                    continue;

                var cleanType = CleanTypeName(field.Type);

                // Ignore self-references (e.g., Node -> Node)
                if (cleanType != cls.Name && definedNames.Contains(cleanType))
                {
                    referencedTypes.Add(cleanType);
                }
            }
        }

        return allClasses.FirstOrDefault(c => !referencedTypes.Contains(c.Name))
               ?? allClasses[0];
    }

    /// <summary>
    /// Generates fake records starting automatically from the detected root class,
    /// with configurable counts for both root records and nested collections.
    /// </summary>
    public List<Dictionary<string, object?>> GenerateFakeData(
        Dictionary<int, ExtractedClass> classes,
        int count = 5,
        int nestedCount = 3,
        string? explicitRootClassName = null)
    {
        var classLookup = classes.Values
            .GroupBy(c => c.Name)
            .ToDictionary(g => g.Key, g => g.First());

        ExtractedClass rootClass;
        if (!string.IsNullOrWhiteSpace(explicitRootClassName))
        {
            if (!classLookup.TryGetValue(explicitRootClassName, out rootClass!))
                throw new ArgumentException($"Class '{explicitRootClassName}' was not found.");
        }
        else
        {
            rootClass = FindRootClass(classes);
        }

        var results = new List<Dictionary<string, object?>>();
        for (int i = 0; i < count; i++)
        {
            results.Add(GenerateInstance(rootClass, classLookup, new HashSet<string>(), nestedCount));
        }

        return results;
    }

    /// <summary>
    /// Generates fake data, serializes it to pretty JSON, saves it to {Guid}.json, and returns the Guid.
    /// </summary>
    public async Task<Guid> GenerateAndSaveJsonAsync(
        Dictionary<int, ExtractedClass> classes,
        int count = 5,
        int nestedCount = 3,
        string outputDirectory = "output",
        string? explicitRootClassName = null)
    {
        var data = GenerateFakeData(classes, count, nestedCount, explicitRootClassName);
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var fullDir = Path.Combine(Directory.GetCurrentDirectory(), outputDirectory);
        Directory.CreateDirectory(fullDir);

        var fileId = Guid.NewGuid();
        var filePath = Path.Combine(fullDir, $"{fileId}.json");
        await File.WriteAllTextAsync(filePath, json);

        return fileId;
    }

    private Dictionary<string, object?> GenerateInstance(
        ExtractedClass currentClass,
        Dictionary<string, ExtractedClass> classLookup,
        HashSet<string> visited,
        int nestedCount)
    {
        var obj = new Dictionary<string, object?>();

        // Guard against infinite recursion on circular references (e.g. Employee -> Employee)
        if (!visited.Add(currentClass.Name))
            return obj;

        foreach (var field in currentClass.Fields)
        {
            obj[field.Name] = GenerateFieldValue(
                field.Name,
                field.Type,
                classLookup,
                new HashSet<string>(visited),
                nestedCount);
        }

        return obj;
    }

    private object? GenerateFieldValue(
        string fieldName,
        string? rawType,
        Dictionary<string, ExtractedClass> classLookup,
        HashSet<string> visited,
        int nestedCount)
    {
        var lowerName = fieldName.ToLowerInvariant();

        // 0. Untyped fields (e.g., JavaScript or untyped Python): infer from field name
        if (string.IsNullOrWhiteSpace(rawType))
            return InferFromFieldName(lowerName);

        var type = rawType.Trim().TrimEnd('?', '*', '&');

        // 1. Handle Maps / Dictionaries (e.g., Map<String, Address>, Dictionary<string, int>)
        if (TryExtractMapValueType(type, out var mapValueType))
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < nestedCount; i++)
            {
                var key = $"{_faker.Lorem.Word()}_{i + 1}";
                dict[key] = GenerateFieldValue(fieldName, mapValueType, classLookup, new HashSet<string>(visited), nestedCount);
            }
            return dict;
        }

        // 2. Handle Arrays / Generic Collections (e.g., Address[], List<Address>, string[])
        if (TryExtractCollectionElementType(type, out var elementType))
        {
            var list = new List<object?>();
            for (int i = 0; i < nestedCount; i++)
            {
                list.Add(GenerateFieldValue(fieldName, elementType, classLookup, new HashSet<string>(visited), nestedCount));
            }
            return list;
        }

        // 3. Handle Nested Custom Class (e.g., Address, Order)
        if (classLookup.TryGetValue(type, out var nestedClass))
        {
            return GenerateInstance(nestedClass, classLookup, visited, nestedCount);
        }

        // 4. Handle Primitives with Smart Field-Name Heuristics
        var lowerType = type.ToLowerInvariant();

        // Booleans
        if (lowerType is "bool" or "boolean")
            return _faker.Random.Bool();

        // Integers
        if (lowerType is "int" or "integer" or "int8" or "int16" or "int32" or "int64"
            or "uint" or "uint8" or "uint16" or "uint32" or "uint64"
            or "long" or "short" or "byte" or "sbyte" or "usize" or "size_t")
        {
            if (lowerName.Contains("age")) return _faker.Random.Int(18, 80);
            if (lowerName.Contains("year")) return _faker.Date.Past(20).Year;
            if (lowerName.Contains("id")) return _faker.Random.Int(1, 10000);
            if (lowerName.Contains("count") || lowerName.Contains("qty") || lowerName.Contains("quantity"))
                return _faker.Random.Int(1, 50);
            return _faker.Random.Int(1, 1000);
        }

        // Floating-point / Decimals
        if (lowerType is "double" or "float" or "float32" or "float64" or "decimal" or "bigdecimal" or "num" or "number")
        {
            if (lowerName.Contains("price") || lowerName.Contains("amount") || lowerName.Contains("salary") || lowerName.Contains("total") || lowerName.Contains("cost"))
                return Math.Round(_faker.Random.Decimal(10, 5000), 2);
            if (lowerName.Contains("rating") || lowerName.Contains("score"))
                return Math.Round(_faker.Random.Double(1, 5), 1);
            if (lowerName.Contains("lat")) return _faker.Address.Latitude();
            if (lowerName.Contains("lon") || lowerName.Contains("lng")) return _faker.Address.Longitude();
            return Math.Round(_faker.Random.Double(1, 500), 2);
        }

        // Strings (matched by common property names)
        if (lowerType is "string" or "str" or "std::string" or "char")
        {
            return GenerateSmartString(lowerName);
        }

        // Dates / Timestamps
        if (lowerType is "datetime" or "datetimeoffset" or "date" or "localdate" or "localdatetime" or "instant" or "time.time" or "dateonly")
            return _faker.Date.Recent(30).ToString("O");

        // Guids / UUIDs
        if (lowerType is "guid" or "uuid")
            return Guid.NewGuid();

        return _faker.Lorem.Word();
    }

    private string GenerateSmartString(string lowerName)
    {
        // Images (checked BEFORE generic "url")
        if (lowerName.Contains("image") || lowerName.Contains("img") ||
            lowerName.Contains("avatar") || lowerName.Contains("photo") ||
            lowerName.Contains("picture") || lowerName.Contains("pic") ||
            lowerName.Contains("thumbnail") || lowerName.Contains("thumb") ||
            lowerName.Contains("banner") || lowerName.Contains("cover") ||
            lowerName.Contains("logo") || lowerName.Contains("icon"))
        {
            return FakeImageProvider.GetImageUrl(_faker, lowerName);
        }

        if (lowerName.Contains("firstname") || lowerName == "first_name") return _faker.Name.FirstName();
        if (lowerName.Contains("lastname") || lowerName == "last_name") return _faker.Name.LastName();
        if (lowerName.Contains("fullname") || lowerName == "name") return _faker.Name.FullName();
        if (lowerName.Contains("username")) return _faker.Internet.UserName();
        if (lowerName.Contains("email")) return _faker.Internet.Email();
        if (lowerName.Contains("password") || lowerName.Contains("pwd")) return _faker.Internet.Password(12);
        if (lowerName.Contains("phone") || lowerName.Contains("mobile") || lowerName.Contains("tel")) return _faker.Phone.PhoneNumber();
        if (lowerName.Contains("company") || lowerName.Contains("organization")) return _faker.Company.CompanyName();
        if (lowerName.Contains("job") || lowerName.Contains("role") || lowerName.Contains("position")) return _faker.Name.JobTitle();
        if (lowerName.Contains("city")) return _faker.Address.City();
        if (lowerName.Contains("country")) return _faker.Address.Country();
        if (lowerName.Contains("state") || lowerName.Contains("province")) return _faker.Address.State();
        if (lowerName.Contains("street") || lowerName.Contains("address")) return _faker.Address.StreetAddress();
        if (lowerName.Contains("zip") || lowerName.Contains("postal")) return _faker.Address.ZipCode();
        if (lowerName.Contains("url") || lowerName.Contains("website") || lowerName.Contains("link")) return _faker.Internet.Url();
        if (lowerName.Contains("ip")) return _faker.Internet.Ip();
        if (lowerName.Contains("color")) return _faker.Internet.Color();
        if (lowerName.Contains("guid") || lowerName.Contains("uuid") || lowerName == "id") return Guid.NewGuid().ToString();
        if (lowerName.Contains("title")) return _faker.Lorem.Sentence(3);
        if (lowerName.Contains("desc") || lowerName.Contains("bio") || lowerName.Contains("summary") || lowerName.Contains("content"))
            return _faker.Lorem.Sentence(10);

        return _faker.Lorem.Word();
    }

    private object InferFromFieldName(string lowerName)
    {
        if (lowerName.StartsWith("is") || lowerName.StartsWith("has") || lowerName is "active" or "enabled" or "verified" or "deleted")
            return _faker.Random.Bool();

        if (lowerName.Contains("age")) return _faker.Random.Int(18, 80);
        if (lowerName == "id" || lowerName.EndsWith("id")) return _faker.Random.Int(1, 10000);
        if (lowerName.Contains("count") || lowerName.Contains("qty") || lowerName.Contains("year")) return _faker.Random.Int(1, 100);
        if (lowerName.Contains("price") || lowerName.Contains("amount") || lowerName.Contains("total") || lowerName.Contains("salary"))
            return Math.Round(_faker.Random.Decimal(10, 5000), 2);

        return GenerateSmartString(lowerName);
    }

    private static bool TryExtractCollectionElementType(string type, out string elementType)
    {
        if (type.EndsWith("[]"))
        {
            elementType = type[..^2].Trim();
            return true;
        }

        // Handles angle-bracket collections and Python-style generic annotations (List[T], list[T]).
        var match = Regex.Match(
            type,
            @"^(?:List|IList|IEnumerable|ICollection|IReadOnlyList|IReadOnlyCollection|Set|HashSet|Array|Collection|std::vector)(?:<(.+)>|\[(.+)\])$",
            RegexOptions.IgnoreCase);

        if (match.Success)
        {
            elementType = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim();
            return true;
        }

        // Handles Go slice []T
        if (type.StartsWith("[]"))
        {
            elementType = type[2..].Trim();
            return true;
        }

        elementType = string.Empty;
        return false;
    }

    private static bool TryExtractMapValueType(string type, out string valueType)
    {
        var match = Regex.Match(
            type,
            @"^(?:Map|HashMap|Dictionary|IDictionary|Record|std::map|std::unordered_map)<[^,]+,\s*(.+)>$",
            RegexOptions.IgnoreCase);

        if (match.Success)
        {
            valueType = match.Groups[1].Value.Trim();
            return true;
        }

        valueType = string.Empty;
        return false;
    }

    private static string CleanTypeName(string rawType)
    {
        var clean = rawType.Trim().TrimEnd('?', '*', '&').Replace("[]", "");

        var start = clean.IndexOfAny(['<', '[']);
        var end = Math.Max(clean.LastIndexOf('>'), clean.LastIndexOf(']'));
        if (start > 0 && end > start)
        {
            var inner = clean[(start + 1)..end].Trim();
            var comma = inner.LastIndexOf(',');
            clean = comma >= 0 ? inner[(comma + 1)..].Trim() : inner;
        }

        return clean;
    }
}
