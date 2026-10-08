using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Reclaim.Core;

namespace Reclaim.Ui;

public sealed class MainViewModel : ObservableObject
{
    const int MinInterval = 1;
    const int MaxInterval = 1440;
    const double MinThresholdGb = 0.5;

    readonly ReclaimService service;
    string usedPercent = "0";
    string inUseText = "";
    string totalText = "";
    string modifiedText = "";
    string standbyText = "";
    string freeText = "";
    string statusText = "";
    string statusKind = "off";
    string nextCleanText = "";
    string thresholdLiveText = "";
    string cleanCountText = "0";
    string totalReclaimedText = "";
    string lastCleanText = "";
    string resultHeadline = "";
    string resultDetail = "";
    string noticeText = "";
    bool isCleaning;

    public MainViewModel(ReclaimService service)
    {
        this.service = service;
        CleanNowCommand = new RelayCommand(() => _ = service.CleanAsync(CleanTrigger.Manual), () => !service.IsCleaning);
        IntervalUpCommand = new RelayCommand(() => IntervalMinutes = StepIntervalUp(IntervalMinutes), () => IntervalMinutes < MaxInterval);
        IntervalDownCommand = new RelayCommand(() => IntervalMinutes = StepIntervalDown(IntervalMinutes), () => IntervalMinutes > MinInterval);
        ThresholdUpCommand = new RelayCommand(() => ThresholdGb = Math.Floor(ThresholdGb * 2 + 1e-9) / 2 + 0.5, () => ThresholdGb < MaxThresholdGb);
        ThresholdDownCommand = new RelayCommand(() => ThresholdGb = Math.Ceiling(ThresholdGb * 2 - 1e-9) / 2 - 0.5, () => ThresholdGb > MinThresholdGb);
        TogglePauseCommand = new RelayCommand(() => service.SetPaused(!service.IsPaused), () => service.HasAutomation);
        QuitCommand = new RelayCommand(() => App.Current.ExitApplication());

        service.Sampled += (_, _) => RefreshLive();
        service.StateChanged += (_, _) =>
        {
            IsCleaning = service.IsCleaning;
            RefreshLive();
            CommandManager.InvalidateRequerySuggested();
        };
        service.Cleaned += (_, e) => ShowResult(e.Result);
        RefreshLive();
    }

    AppSettings S => service.Settings;

    double MaxThresholdGb => Math.Max(1, Math.Floor(service.Current.Total / (1024d * 1024 * 1024)));

    public ICommand CleanNowCommand { get; }
    public ICommand IntervalUpCommand { get; }
    public ICommand IntervalDownCommand { get; }
    public ICommand ThresholdUpCommand { get; }
    public ICommand ThresholdDownCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand QuitCommand { get; }

    public string VersionText { get; } = $"Version {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";

    public string UsedPercent { get => usedPercent; private set => Set(ref usedPercent, value); }
    public string InUseText { get => inUseText; private set => Set(ref inUseText, value); }
    public string TotalText { get => totalText; private set => Set(ref totalText, value); }
    public string ModifiedText { get => modifiedText; private set => Set(ref modifiedText, value); }
    public string StandbyText { get => standbyText; private set => Set(ref standbyText, value); }
    public string FreeText { get => freeText; private set => Set(ref freeText, value); }
    public string StatusText { get => statusText; private set => Set(ref statusText, value); }
    public string StatusKind { get => statusKind; private set => Set(ref statusKind, value); }
    public string NextCleanText { get => nextCleanText; private set => Set(ref nextCleanText, value); }
    public string ThresholdLiveText { get => thresholdLiveText; private set => Set(ref thresholdLiveText, value); }
    public string CleanCountText { get => cleanCountText; private set => Set(ref cleanCountText, value); }
    public string TotalReclaimedText { get => totalReclaimedText; private set => Set(ref totalReclaimedText, value); }
    public string LastCleanText { get => lastCleanText; private set => Set(ref lastCleanText, value); }
    public string ResultHeadline { get => resultHeadline; private set => Set(ref resultHeadline, value); }
    public string ResultDetail { get => resultDetail; private set => Set(ref resultDetail, value); }
    public string NoticeText { get => noticeText; private set => Set(ref noticeText, value); }
    public bool IsCleaning { get => isCleaning; private set => Set(ref isCleaning, value); }

    public bool ScheduleEnabled
    {
        get => S.ScheduleEnabled;
        set => Apply(S.ScheduleEnabled != value, () => S.ScheduleEnabled = value, service.Reschedule);
    }

    public int IntervalMinutes
    {
        get => S.IntervalMinutes;
        set
        {
            int clamped = Math.Clamp(value, MinInterval, MaxInterval);
            Apply(S.IntervalMinutes != clamped, () => S.IntervalMinutes = clamped, service.Reschedule);
        }
    }

    public bool ThresholdEnabled
    {
        get => S.ThresholdEnabled;
        set => Apply(S.ThresholdEnabled != value, () => S.ThresholdEnabled = value, service.ResetThresholdBackoff);
    }

    public bool ThresholdIsStandby
    {
        get => S.ThresholdMetric == ThresholdMetric.Standby;
        set { if (value) SetMetric(ThresholdMetric.Standby); }
    }

    public bool ThresholdIsFree
    {
        get => S.ThresholdMetric == ThresholdMetric.Free;
        set { if (value) SetMetric(ThresholdMetric.Free); }
    }

    public double ThresholdGb
    {
        get => S.ThresholdGb;
        set
        {
            double clamped = Math.Round(Math.Clamp(value, MinThresholdGb, MaxThresholdGb), 1);
            Apply(Math.Abs(S.ThresholdGb - clamped) > 0.001, () => S.ThresholdGb = clamped, service.ResetThresholdBackoff);
        }
    }

    public bool CleanStandby { get => S.CleanStandby; set => SetTarget(value, S.CleanStandby, v => S.CleanStandby = v); }
    public bool CleanModified { get => S.CleanModified; set => SetTarget(value, S.CleanModified, v => S.CleanModified = v); }
    public bool CleanWorkingSets { get => S.CleanWorkingSets; set => SetTarget(value, S.CleanWorkingSets, v => S.CleanWorkingSets = v); }
    public bool CleanFileCache { get => S.CleanFileCache; set => SetTarget(value, S.CleanFileCache, v => S.CleanFileCache = v); }

    public bool StartSilently { get => S.StartSilently; set => Apply(S.StartSilently != value, () => S.StartSilently = value); }
    public bool MinimizeToTray { get => S.MinimizeToTray; set => Apply(S.MinimizeToTray != value, () => S.MinimizeToTray = value); }
    public bool CloseToTray { get => S.CloseToTray; set => Apply(S.CloseToTray != value, () => S.CloseToTray = value); }
    public bool Notifications { get => S.Notifications; set => Apply(S.Notifications != value, () => S.Notifications = value); }

    public bool RunAtStartup
    {
        get => S.RunAtStartup;
        set
        {
            if (S.RunAtStartup == value)
                return;
            try
            {
                if (value)
                    StartupManager.Register();
                else
                    StartupManager.Unregister();
                S.RunAtStartup = value;
                SettingsStore.Save(S);
                NoticeText = "";
            }
            catch (Exception ex)
            {
                NoticeText = $"Couldn't update the sign-in task. {ex.Message}";
            }
            OnPropertyChanged();
        }
    }

    public void NotifyStartupChanged() => OnPropertyChanged(nameof(RunAtStartup));

    void Apply(bool changed, Action change, Action? after = null, [CallerMemberName] string? name = null)
    {
        if (changed)
        {
            change();
            SettingsStore.Save(S);
            after?.Invoke();
        }
        OnPropertyChanged(name);
        RefreshLive();
    }

    void SetMetric(ThresholdMetric metric)
    {
        Apply(S.ThresholdMetric != metric, () => S.ThresholdMetric = metric, service.ResetThresholdBackoff, nameof(ThresholdIsStandby));
        OnPropertyChanged(nameof(ThresholdIsFree));
    }

    void SetTarget(bool value, bool current, Action<bool> change, [CallerMemberName] string? name = null)
    {
        bool lastOne = !value && current && CountTargets() == 1;
        Apply(value != current && !lastOne, () => change(value), null, name);
        NoticeText = lastOne ? "At least one memory area must stay selected." : "";
    }

    int CountTargets() => new[] { S.CleanStandby, S.CleanModified, S.CleanWorkingSets, S.CleanFileCache }.Count(on => on);

    void RefreshLive()
    {
        var s = service.Current;
        UsedPercent = $"{Math.Round(s.UsedRatio * 100):0}";
        InUseText = Format.Bytes(s.InUse);
        TotalText = Format.Bytes(s.Total);
        ModifiedText = Format.Bytes(s.Modified);
        StandbyText = Format.Bytes(s.Standby);
        FreeText = Format.Bytes(s.Free);
        ThresholdLiveText = Format.Bytes(service.ThresholdSubject(s));
        CleanCountText = $"{S.CleanCount:N0}";
        TotalReclaimedText = S.TotalReclaimedBytes > 0 ? Format.Bytes(S.TotalReclaimedBytes) : "0 MB";
        LastCleanText = S.LastCleanUtc is { } last ? Format.Ago(last) : "Never";
        RefreshStatus();
    }

    void RefreshStatus()
    {
        var remaining = service.NextScheduledUtc is { } next ? next - DateTime.UtcNow : (TimeSpan?)null;
        string limit = Format.Gigabytes(S.ThresholdGb);

        NextCleanText = !S.ScheduleEnabled ? "Off" : service.IsPaused ? "Paused" : remaining is { } r ? Format.Countdown(r) : "—";

        if (service.IsCleaning)
            (StatusKind, StatusText) = ("busy", "Reclaiming memory…");
        else if (!service.HasAutomation)
            (StatusKind, StatusText) = ("off", "Automatic cleaning off");
        else if (service.IsPaused)
            (StatusKind, StatusText) = ("paused", "Paused · click to resume");
        else if (S.ScheduleEnabled && remaining is { } left)
            (StatusKind, StatusText) = ("active", $"Next clean in {Format.Countdown(left)}");
        else
            (StatusKind, StatusText) = ("watching", S.ThresholdMetric == ThresholdMetric.Standby
                ? $"Watching standby ≥ {limit}"
                : $"Watching free ≤ {limit}");
    }

    void ShowResult(CleanResult result)
    {
        ResultHeadline = result.FreedBytes >= 1024 * 1024 ? $"Reclaimed {Format.Bytes(result.FreedBytes)}" : "Already optimal";
        string detail = $"{Describe(result.Performed)} · {Format.Duration(result.Duration)}";
        if (result.Failed != CleanTargets.None)
            detail += $" · {Describe(result.Failed)} failed";
        ResultDetail = detail;
        RefreshLive();
    }

    public static string Describe(CleanTargets targets)
    {
        var parts = new List<string>();
        if (targets.HasFlag(CleanTargets.StandbyList)) parts.Add("Standby list");
        if (targets.HasFlag(CleanTargets.ModifiedList)) parts.Add("Modified list");
        if (targets.HasFlag(CleanTargets.WorkingSets)) parts.Add("Working sets");
        if (targets.HasFlag(CleanTargets.FileCache)) parts.Add("File cache");
        return parts.Count == 0 ? "Nothing" : string.Join(" + ", parts);
    }

    static int StepIntervalUp(int minutes) => minutes < 10 ? minutes + 1 : minutes < 60 ? (minutes / 5 + 1) * 5 : (minutes / 15 + 1) * 15;

    static int StepIntervalDown(int minutes) => minutes <= 10 ? minutes - 1 : minutes <= 60 ? (minutes - 1) / 5 * 5 : (minutes - 1) / 15 * 15;
}
