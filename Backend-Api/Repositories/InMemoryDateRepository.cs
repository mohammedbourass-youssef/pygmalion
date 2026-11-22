namespace Backend_Api.Repositories;

public sealed class InMemoryDateRepository : IDateRepository
{
    private static readonly DateTime StartDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EndDate = new(2030, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    public DateTime GenerateDate()
    {
        var rangeTicks = EndDate.Ticks - StartDate.Ticks;
        return StartDate.AddTicks(Random.Shared.NextInt64(rangeTicks + 1));
    }
}
