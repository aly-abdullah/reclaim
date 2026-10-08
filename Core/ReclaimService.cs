using System.Windows.Threading;

namespace Reclaim.Core;

public enum CleanTrigger
{
    Manual,
    Schedule,
    Threshold
}

public sealed class CleanCompletedEventArgs(CleanResult result, CleanTrigger trigger) : EventArgs
{
    public CleanResult Result { get; } = result;
    public CleanTrigger Trigger { get; } = trigger;
}

public sealed class ReclaimService
{
    public const int HistoryCapacity = 180;
    const double BytesPerGb = 1024d * 1024 * 1024;
    const long IneffectiveBytes = 64L * 1024 * 1024;
    static readonly TimeSpan MinBackoff = TimeSpan.FromMinutes(1);
    static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(30);

    readonly DispatcherTimer timer;
    readonly double[] inUseHistory = new double[HistoryCapacity];
    readonly double[] standbyHistory = new double[HistoryCapacity];
    readonly List<long> cleanMarks = [];
    int historyHead;
    int historyCount;
    long sampleCount;
    TimeSpan thresholdBackoff = MinBackoff;
    DateTime lastThresholdCleanUtc = DateTime.MinValue;

    public ReclaimService(AppSettings settings)
    {
        Settings = settings;
        Current = MemoryInfo.Read();
        Push(Current);
        timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += OnTick;
        Reschedule();
    }

    public AppSettings Settings { get; }
    public MemorySnapshot Current { get; private set; }
    public DateTime? NextScheduledUtc { get; private set; }
    public bool IsCleaning { get; private set; }
    public bool IsPaused { get; private set; }
    public bool HasAutomation => Settings.ScheduleEnabled || Settings.ThresholdEnabled;

    public event EventHandler? Sampled;
    public event EventHandler? StateChanged;
    public event EventHandler<CleanCompletedEventArgs>? Cleaned;

    public void Start() => timer.Start();

    public void Stop() => timer.Stop();

    public void Reschedule()
    {
        NextScheduledUtc = Settings.ScheduleEnabled ? DateTime.UtcNow.AddMinutes(Settings.IntervalMinutes) : null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetThresholdBackoff()
    {
        thresholdBackoff = MinBackoff;
        lastThresholdCleanUtc = DateTime.MinValue;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused)
            return;
        IsPaused = paused;
        if (!paused)
        {
            Reschedule();
            ResetThresholdBackoff();
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public ulong ThresholdBytes => (ulong)(Settings.ThresholdGb * BytesPerGb);

    public ulong ThresholdSubject(MemorySnapshot snapshot) =>
        Settings.ThresholdMetric == ThresholdMetric.Standby ? snapshot.Standby : snapshot.Free;

    public bool ThresholdReached(MemorySnapshot snapshot) =>
        Settings.ThresholdMetric == ThresholdMetric.Standby
            ? snapshot.Standby >= ThresholdBytes
            : snapshot.Free <= ThresholdBytes;

    public async Task<CleanResult?> CleanAsync(CleanTrigger trigger)
    {
        if (IsCleaning)
            return null;

        IsCleaning = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            var targets = Settings.Targets;
            var result = await Task.Run(() => MemoryCleaner.Clean(targets));
            var now = DateTime.UtcNow;

            Settings.CleanCount++;
            Settings.TotalReclaimedBytes += result.FreedBytes;
            Settings.LastCleanUtc = now;
            SettingsStore.Save(Settings);

            if (trigger == CleanTrigger.Threshold)
            {
                lastThresholdCleanUtc = now;
                thresholdBackoff = result.FreedBytes < IneffectiveBytes
                    ? TimeSpan.FromTicks(Math.Min(thresholdBackoff.Ticks * 2, MaxBackoff.Ticks))
                    : MinBackoff;
            }

            if (Settings.ScheduleEnabled)
                NextScheduledUtc = now.AddMinutes(Settings.IntervalMinutes);

            cleanMarks.Add(sampleCount);
            Current = result.After;
            Cleaned?.Invoke(this, new CleanCompletedEventArgs(result, trigger));
            return result;
        }
        finally
        {
            IsCleaning = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int CopyHistory(double[] inUse, double[] standby)
    {
        for (int i = 0; i < historyCount; i++)
        {
            int index = (historyHead - historyCount + i + HistoryCapacity) % HistoryCapacity;
            inUse[i] = inUseHistory[index];
            standby[i] = standbyHistory[index];
        }
        return historyCount;
    }

    public IReadOnlyList<int> MarkPositions(int count)
    {
        var positions = new List<int>();
        cleanMarks.RemoveAll(mark => sampleCount - 1 - mark >= HistoryCapacity);
        foreach (long mark in cleanMarks)
        {
            long age = sampleCount - 1 - mark;
            if (age >= 0 && age < count)
                positions.Add(count - 1 - (int)age);
        }
        return positions;
    }

    void OnTick(object? sender, EventArgs e)
    {
        Current = MemoryInfo.Read();
        Push(Current);
        Sampled?.Invoke(this, EventArgs.Empty);

        if (IsCleaning || IsPaused)
            return;

        var now = DateTime.UtcNow;
        if (Settings.ScheduleEnabled && NextScheduledUtc is { } next && now >= next)
        {
            _ = CleanAsync(CleanTrigger.Schedule);
            return;
        }

        if (!Settings.ThresholdEnabled)
            return;

        if (!ThresholdReached(Current))
        {
            thresholdBackoff = MinBackoff;
            return;
        }

        if (now - lastThresholdCleanUtc >= thresholdBackoff)
            _ = CleanAsync(CleanTrigger.Threshold);
    }

    void Push(MemorySnapshot snapshot)
    {
        inUseHistory[historyHead] = snapshot.UsedRatio;
        standbyHistory[historyHead] = snapshot.Fraction(snapshot.Standby);
        historyHead = (historyHead + 1) % HistoryCapacity;
        historyCount = Math.Min(historyCount + 1, HistoryCapacity);
        sampleCount++;
    }
}
