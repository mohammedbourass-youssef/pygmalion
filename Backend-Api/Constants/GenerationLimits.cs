namespace Backend_Api.Constants;

public static class GenerationLimits
{
    public const int MaxRootCount = 500;
    public const int MaxNestedCount = 20;

    public static int ClampRootCount(int value) => Math.Clamp(value, 1, MaxRootCount);

    public static int ClampNestedCount(int value) => Math.Clamp(value, 1, MaxNestedCount);
}
