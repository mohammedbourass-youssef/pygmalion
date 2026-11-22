using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend_Api.Data;
using Backend_Api.Dtos;
using Backend_Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backend_Api.Services;

public sealed class GeneratedDataService(
    PygmalionDbContext dbContext,
    IFakeDataGenerator fakeDataGenerator,
    IConfiguration configuration) : IGeneratedDataService
{
    private readonly string _outputDirectory = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(),
        configuration["GeneratedData:OutputDirectory"] ?? "output"));

    public async Task<DataGenerated> GenerateOrReuseAsync(
        string language,
        string code,
        Dictionary<int, ExtractedClass> classes,
        int count,
        int nestedCount,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = language.Trim().ToUpperInvariant();
        var inputIdentity = JsonSerializer.Serialize(new
        {
            language = normalizedLanguage,
            code,
            count,
            nestedCount
        });
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputIdentity)));

        var existing = await dbContext.DataGenerated.SingleOrDefaultAsync(
            item => item.CodeHash == codeHash,
            cancellationToken);
        if (existing is not null)
        {
            if (!File.Exists(existing.FilePath))
                await WritePayloadAsync(existing.FilePath, classes, count, nestedCount, cancellationToken);
            return existing;
        }

        var rootClass = fakeDataGenerator.FindRootClass(classes);
        var metadata = JsonSerializer.Serialize(new
        {
            language,
            rootClass = rootClass.Name,
            recordCount = count,
            nestedCount,
            classNames = classes.Values.Select(item => item.Name).Distinct().ToArray()
        });
        var id = Guid.NewGuid();
        var filePath = Path.Combine(_outputDirectory, $"{id:D}.json");
        var record = new DataGenerated
        {
            Id = id,
            Metadata = metadata,
            DateCreated = DateTimeOffset.UtcNow,
            FilePath = filePath,
            CodeHash = codeHash
        };

        dbContext.DataGenerated.Add(record);
        try
        {
            // Reserve the unique hash before creating a file. Concurrent duplicate requests
            // converge on the winning database row and therefore the same GUID/path.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.Entry(record).State = EntityState.Detached;
            var winner = await dbContext.DataGenerated.SingleOrDefaultAsync(
                item => item.CodeHash == codeHash,
                cancellationToken);
            if (winner is null)
                throw;
            if (!File.Exists(winner.FilePath))
                await WritePayloadAsync(winner.FilePath, classes, count, nestedCount, cancellationToken);
            return winner;
        }

        try
        {
            await WritePayloadAsync(filePath, classes, count, nestedCount, cancellationToken);
            return record;
        }
        catch
        {
            dbContext.DataGenerated.Remove(record);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task WritePayloadAsync(
        string filePath,
        Dictionary<int, ExtractedClass> classes,
        int count,
        int nestedCount,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(
            fakeDataGenerator.GenerateFakeData(classes, count, nestedCount),
            new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
