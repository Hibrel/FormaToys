using FormaToys.Core.Media;

namespace FormaToys.App.ViewModels;

public static class DisplayFormat
{
    public static string Bytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "—";
        }

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }

    public static string Duration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return string.Empty;
        }

        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes}:{duration.Seconds:00}";
    }

    public static string Meta(MediaInfo info)
    {
        var parts = new List<string>();

        if (info.Kind == MediaKind.Image && info.Width > 0 && info.Height > 0)
        {
            parts.Add($"{info.Width} × {info.Height}");
        }
        else if (info.Kind == MediaKind.Video && info.Width > 0 && info.Height > 0)
        {
            var fps = info.Fps >= 1 ? $" · {info.Fps:0.##} fps" : string.Empty;
            parts.Add($"{info.Width} × {info.Height}{fps}");
        }

        var duration = Duration(info.Duration);
        if (!string.IsNullOrEmpty(duration))
        {
            parts.Add(duration);
        }

        var codec = info.Kind == MediaKind.Audio ? info.AudioCodec : info.VideoCodec;
        if (!string.IsNullOrEmpty(codec))
        {
            parts.Add(codec.ToUpperInvariant());
        }

        parts.Add(Bytes(info.SizeBytes));
        return string.Join(" · ", parts);
    }
}
