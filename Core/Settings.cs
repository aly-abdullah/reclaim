using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reclaim.Core;

public enum ThresholdMetric
{
    Standby,
    Free
}

public sealed class AppSettings
{
    public bool ScheduleEnabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public bool ThresholdEnabled { get; set; }
    public ThresholdMetric ThresholdMetric { get; set; } = ThresholdMetric.Standby;
    public double ThresholdGb { get; set; } = 4;

    public bool CleanStandby { get; set; } = true;
    public bool CleanModified { get; set; }
    public bool CleanWorkingSets { get; set; }
    public bool CleanFileCache { get; set; }

    public bool RunAtStartup { get; set; }
    public bool StartSilently { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool Notifications { get; set; } = true;
    public bool TrayHintShown { get; set; }

    public int CleanCount { get; set; }
    public long TotalReclaimedBytes { get; set; }
    public DateTime? LastCleanUtc { get; set; }

    [JsonIgnore]
    public CleanTargets Targets
    {
        get
        {
            var targets = CleanTargets.None;
            if (CleanWorkingSets) targets |= CleanTargets.WorkingSets;
            if (CleanFileCache) targets |= CleanTargets.FileCache;
            if (CleanModified) targets |= CleanTargets.ModifiedList;
            if (CleanStandby) targets |= CleanTargets.StandbyList;
            return targets == CleanTargets.None ? CleanTargets.StandbyList : targets;
        }
    }
}

public static class SettingsStore
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Folder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Reclaim");
    static string FilePath => Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            File.Move(temp, FilePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
