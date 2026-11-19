using System.Globalization;

namespace FormaToys.Core.Conversion;

/// <summary>
/// 解析 ffmpeg -progress 的 key=value 输出。
/// ffmpeg 的 out_time_us / out_time_ms 两个字段实际上都是“微秒”，别被名字骗了。
/// </summary>
public static class ProgressParser
{
    public static bool TryParseTimestamp(string line, out TimeSpan elapsed)
    {
        elapsed = TimeSpan.Zero;
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        var span = line.AsSpan().Trim();
        if (span.StartsWith("out_time_us=", StringComparison.Ordinal))
        {
            return TryMicroseconds(span["out_time_us=".Length..], out elapsed);
        }

        if (span.StartsWith("out_time_ms=", StringComparison.Ordinal))
        {
            return TryMicroseconds(span["out_time_ms=".Length..], out elapsed);
        }

        if (span.StartsWith("out_time=", StringComparison.Ordinal))
        {
            return TimeSpan.TryParse(span["out_time=".Length..], CultureInfo.InvariantCulture, out elapsed);
        }

        return false;
    }

    public static bool IsFinished(string line) =>
        line.Trim().Equals("progress=end", StringComparison.OrdinalIgnoreCase);

    private static bool TryMicroseconds(ReadOnlySpan<char> text, out TimeSpan elapsed)
    {
        elapsed = TimeSpan.Zero;
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds) && microseconds >= 0)
        {
            elapsed = TimeSpan.FromTicks(microseconds * 10);
            return true;
        }

        return false;
    }
}
