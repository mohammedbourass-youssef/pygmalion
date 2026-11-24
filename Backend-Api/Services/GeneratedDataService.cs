using System.Diagnostics;
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
    IConfiguration configuration,
    ILogger<GeneratedDataService> logger) : IGeneratedDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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

        var lookupTimer = Stopwatch.StartNew();
        DataGenerated? existing;
        using (PygmalionTelemetry.ActivitySource.StartActivity("pygmalion.deduplication.lookup"))
        {
            existing = await dbContext.DataGenerated.SingleOrDefaultAsync(
                item => item.CodeHash == codeHash,
                cancellationToken);
        }
        lookupTimer.Stop();
        PygmalionTelemetry.RecordPhase(normalizedLanguage, "dedupe_lookup", lookupTimer.Elapsed.TotalMilliseconds);

        if (existing is not null)
        {
            PygmalionTelemetry.RecordReuse(normalizedLanguage);
            logger.LogInformation(
                "Reusing saved generation. Language={Language} FileId={FileId} CodeHash={CodeHash} LookupDurationMs={LookupDurationMs}",
                normalizedLanguage,
                existing.Id,
                codeHash,
                lookupTimer.Elapsed.TotalMilliseconds);
            if (!File.Exists(existing.FilePath))
                await WritePayloadAsync(existing.FilePath, classes, normalizedLanguage, count, nestedCount, cancellationToken);
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
            var reserveTimer = Stopwatch.StartNew();
            await dbContext.SaveChangesAsync(cancellationToken);
            reserveTimer.Stop();
            PygmalionTelemetry.RecordPhase(normalizedLanguage, "database_reservation", reserveTimer.Elapsed.TotalMilliseconds);
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
                await WritePayloadAsync(winner.FilePath, classes, normalizedLanguage, count, nestedCount, cancellationToken);
            return winner;
        }

        try
        {
            await WritePayloadAsync(filePath, classes, normalizedLanguage, count, nestedCount, cancellationToken);
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
        string language,
        int count,
        int nestedCount,
        CancellationToken cancellationToken)
    {
        var generateTimer = Stopwatch.StartNew();
        List<Dictionary<string, object?>> data;
        using (PygmalionTelemetry.ActivitySource.StartActivity("pygmalion.fake_data.generate"))
        {
            data = fakeDataGenerator.GenerateFakeData(classes, count, nestedCount);
        }
        generateTimer.Stop();
        PygmalionTelemetry.RecordPhase(language, "fake_data_generation", generateTimer.Elapsed.TotalMilliseconds);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var serializeWriteTimer = Stopwatch.StartNew();
        await using (var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            using (PygmalionTelemetry.ActivitySource.StartActivity("pygmalion.json.serialize_write"))
            {
                await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }
        serializeWriteTimer.Stop();
        PygmalionTelemetry.RecordPhase(language, "serialize_write", serializeWriteTimer.Elapsed.TotalMilliseconds);
        var fileBytes = new FileInfo(filePath).Length;
        PygmalionTelemetry.RecordOutputBytes(language, fileBytes);

        logger.LogInformation(
            "Generated JSON file. Language={Language} RootRecords={RootRecords} NestedCount={NestedCount} FileBytes={FileBytes} GenerateDurationMs={GenerateDurationMs} SerializeWriteDurationMs={SerializeWriteDurationMs} FilePath={FilePath}",
            language,
            data.Count,
            nestedCount,
            fileBytes,
            generateTimer.Elapsed.TotalMilliseconds,
            serializeWriteTimer.Elapsed.TotalMilliseconds,
            filePath);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
