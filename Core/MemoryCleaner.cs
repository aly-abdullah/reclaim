using System.Diagnostics;

namespace Reclaim.Core;

[Flags]
public enum CleanTargets
{
    None = 0,
    WorkingSets = 1,
    FileCache = 2,
    ModifiedList = 4,
    StandbyList = 8
}

public sealed record CleanResult(long FreedBytes, CleanTargets Performed, CleanTargets Failed, TimeSpan Duration, MemorySnapshot After);

public static class MemoryCleaner
{
    public static CleanResult Clean(CleanTargets targets)
    {
        var watch = Stopwatch.StartNew();
        var before = MemoryInfo.Read();
        var performed = CleanTargets.None;
        var failed = CleanTargets.None;

        void Run(CleanTargets target, Func<bool> action)
        {
            if (!targets.HasFlag(target))
                return;
            if (action())
                performed |= target;
            else
                failed |= target;
        }

        Run(CleanTargets.WorkingSets, () => Command(Native.MemoryEmptyWorkingSets));
        Run(CleanTargets.FileCache, () => Native.SetSystemFileCacheSize(-1, -1, 0));
        Run(CleanTargets.ModifiedList, () => Command(Native.MemoryFlushModifiedList));
        Run(CleanTargets.StandbyList, () => Command(Native.MemoryPurgeStandbyList));

        var after = MemoryInfo.Read();
        long freed = (long)after.Free - (long)before.Free;
        return new CleanResult(Math.Max(0, freed), performed, failed, watch.Elapsed, after);
    }

    static bool Command(int command) =>
        Native.NtSetSystemInformation(Native.SystemMemoryListInformation, ref command, sizeof(int)) == 0;
}
