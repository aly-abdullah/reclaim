namespace Reclaim.Core;

public static class Format
{
    const double Kb = 1024;
    const double Mb = Kb * 1024;
    const double Gb = Mb * 1024;
    const double Tb = Gb * 1024;

    public static string Bytes(double bytes) => bytes switch
    {
        >= Tb => $"{bytes / Tb:0.00} TB",
        >= Gb * 100 => $"{bytes / Gb:0} GB",
        >= Gb => $"{bytes / Gb:0.0} GB",
        >= Mb => $"{bytes / Mb:0} MB",
        _ => $"{bytes / Kb:0} KB"
    };

    public static string Gigabytes(double gb) => $"{gb:0.#} GB";

    public static string Countdown(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }

    public static string Duration(TimeSpan span) =>
        span.TotalSeconds < 1 ? $"{span.TotalMilliseconds:0} ms" : $"{span.TotalSeconds:0.0} s";

    public static string Ago(DateTime utc)
    {
        var age = DateTime.UtcNow - utc;
        if (age.TotalSeconds < 45) return "Just now";
        if (age.TotalMinutes < 60) return $"{Math.Max(1, (int)Math.Round(age.TotalMinutes))} min ago";
        if (age.TotalHours < 24) return $"{(int)age.TotalHours} h ago";
        if (age.TotalHours < 48) return "Yesterday";
        return utc.ToLocalTime().ToString("MMM d");
    }

    public static string Interval(int minutes) => minutes switch
    {
        < 60 => $"{minutes} min",
        _ when minutes % 60 == 0 => minutes == 60 ? "1 hour" : $"{minutes / 60} hours",
        _ => $"{minutes / 60} h {minutes % 60} min"
    };
}
