namespace Maui.SmartImage.Services;

/// <summary>
/// Prevents stale async loads from overwriting newer UI state.
/// </summary>
internal sealed class LoadGenerationGuard
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
