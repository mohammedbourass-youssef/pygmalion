using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Backend_Api.Services;

public static class PygmalionTelemetry
{
    public const string ActivitySourceName = "Pygmalion.Generation";
    public const string MeterName = "Pygmalion.Generation";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Histogram<double> PhaseDuration = Meter.CreateHistogram<double>(
        "pygmalion.generation.phase.duration", "ms", "Duration of generation pipeline phases.");
    private static readonly Histogram<long> RootRecords = Meter.CreateHistogram<long>(
        "pygmalion.generation.root.records", "1", "Root records requested per generation.");
    private static readonly Histogram<long> NestedCount = Meter.CreateHistogram<long>(
        "pygmalion.generation.nested.count", "1", "Nested items requested per collection.");
    private static readonly Histogram<long> OutputBytes = Meter.CreateHistogram<long>(
        "pygmalion.generation.output.size", "By", "Generated JSON file size in bytes.");
    private static readonly Counter<long> GenerationRequests = Meter.CreateCounter<long>(
        "pygmalion.generation.requests", "1", "Successful fake-data generation requests.");
    private static readonly Counter<long> GenerationFailures = Meter.CreateCounter<long>(
        "pygmalion.generation.failures", "1", "Failed fake-data generation requests.");
    private static readonly Counter<long> CappedRequests = Meter.CreateCounter<long>(
        "pygmalion.generation.capped", "1", "Requests whose counts exceeded public caps.");

    public static void RecordPhase(string language, string phase, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "language", language },
            { "phase", phase }
        };
        PhaseDuration.Record(elapsedMilliseconds, tags);
    }

    public static void RecordRequestedSizes(
        string language,
        int requestedRootCount,
        int requestedNestedCount,
        int effectiveRootCount,
        int effectiveNestedCount)
    {
        var requestedTags = new TagList
        {
            { "language", language },
            { "stage", "requested" }
        };
        RootRecords.Record(requestedRootCount, requestedTags);
        NestedCount.Record(requestedNestedCount, requestedTags);

        var effectiveTags = new TagList
        {
            { "language", language },
            { "stage", "effective" }
        };
        RootRecords.Record(effectiveRootCount, effectiveTags);
        NestedCount.Record(effectiveNestedCount, effectiveTags);

        if (requestedRootCount != effectiveRootCount || requestedNestedCount != effectiveNestedCount)
            CappedRequests.Add(1, new TagList { { "language", language } });
    }

    public static void RecordOutputBytes(string language, long bytes) =>
        OutputBytes.Record(bytes, new TagList { { "language", language } });

    public static void RecordRequest(string language) =>
        GenerationRequests.Add(1, new TagList { { "language", language } });

    public static void RecordFailure(string language, string phase) =>
        GenerationFailures.Add(1, new TagList { { "language", language }, { "phase", phase } });
}
