namespace Maui.SmartImage.Services;

public sealed class LoadGenerationGuard
{
    private long _current;

    public long Begin()
    {
        return Interlocked.Increment(ref _current);
    }

    public bool IsCurrent(long generation)
    {
        return Interlocked.Read(ref _current) == generation;
    }
}
