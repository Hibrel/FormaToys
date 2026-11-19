using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormaToys.Core.Media;

/// <summary>用随包的 ffprobe 读元数据；探测失败不抛异常，返回 Unknown 的兜底信息。</summary>
public sealed class MediaProbe
{
    private readonly Tooling.ToolPaths _tools;

    public MediaProbe(Tooling.ToolPaths tools) => _tools = tools;

    public async Task<MediaInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        var fallback = new MediaInfo
        {
            Path = path,
            Kind = GuessKindFromExtension(path),
            SizeBytes = SafeFileLength(path),
        };

        if (!File.Exists(path))
        {
            return fallback;
        }

        try
        {
            var psi = new ProcessStartInfo(_tools.FfprobePath)
            {
                WorkingDirectory = _tools.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in new[]
                     {
                         "-v", "error",
                         "-print_format", "json",
                         "-show_format",
                         "-show_streams",
                         "-i", path,
                     })
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = psi };
            process.Start();
            var json = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            _ = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(json))
            {
                return fallback;
            }

            return Parse(path, json, fallback);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return fallback;
        }
    }

    private static MediaInfo Parse(string path, string json, MediaInfo fallback)
    {
        ProbeRoot? root = JsonSerializer.Deserialize<ProbeRoot>(json);
        if (root is null)
        {
            return fallback;
        }

        var streams = root.Streams ?? new List<ProbeStream>();
        var video = streams.FirstOrDefault(s =>
            string.Equals(s.CodecType, "video", StringComparison.OrdinalIgnoreCase) && s.Disposition?.AttachedPic != 1);
        var audio = streams.FirstOrDefault(s =>
            string.Equals(s.CodecType, "audio", StringComparison.OrdinalIgnoreCase));

        var formatName = root.Format?.FormatName ?? string.Empty;
        var durationSeconds = ParseDouble(root.Format?.Duration) ?? ParseDouble(video?.Duration) ?? 0d;

        var frameCount = ParseLong(video?.NbFrames) ?? 0;
        var isAnimatedGif = string.Equals(video?.CodecName, "gif", StringComparison.OrdinalIgnoreCase)
                            && (frameCount > 1 || durationSeconds >= 1d);

        MediaKind kind;
        if (!isAnimatedGif && IsImageFormat(formatName, video))
        {
            kind = MediaKind.Image;
        }
        else if (video is not null)
        {
            kind = MediaKind.Video;
        }
        else if (audio is not null)
        {
            kind = MediaKind.Audio;
        }
        else
        {
            kind = fallback.Kind;
        }

        var fps = ParseFrameRate(video?.AvgFrameRate) ?? ParseFrameRate(video?.RFrameRate) ?? 0d;

        return new MediaInfo
        {
            Path = path,
            Kind = kind,
            Duration = TimeSpan.FromSeconds(Math.Max(0d, durationSeconds)),
            VideoCodec = video?.CodecName,
            AudioCodec = audio?.CodecName,
            Width = video?.Width ?? 0,
            Height = video?.Height ?? 0,
            Fps = fps,
            SizeBytes = ParseLong(root.Format?.Size) ?? fallback.SizeBytes,
            Container = formatName,
            AudioChannels = audio?.Channels ?? 0,
            AudioSampleRate = int.TryParse(audio?.SampleRate, out var sr) ? sr : 0,
        };
    }

    private static bool IsImageFormat(string formatName, ProbeStream? video)
    {
        if (video is null)
        {
            return false;
        }

        if (formatName.Contains("image2", StringComparison.OrdinalIgnoreCase) ||
            formatName.Contains("_pipe", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var codec = video.CodecName?.ToLowerInvariant();
        return codec is "png" or "mjpeg" or "bmp" or "webp" or "tiff" or "targa" or "gif";
    }

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null;

    private static double? ParseFrameRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) &&
            den > 0)
        {
            return num / den;
        }

        return ParseDouble(value);
    }

    private static long SafeFileLength(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static MediaKind GuessKindFromExtension(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".mp4" or ".mkv" or ".mov" or ".avi" or ".webm" or ".m4v" or ".wmv" or ".flv" or ".ts" or ".gif" or ".mts" => MediaKind.Video,
            ".mp3" or ".m4a" or ".aac" or ".wav" or ".flac" or ".ogg" or ".opus" or ".wma" or ".aiff" or ".alac" => MediaKind.Audio,
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp" or ".tif" or ".tiff" or ".heic" => MediaKind.Image,
            _ => MediaKind.Unknown,
        };
    }

    private sealed class ProbeRoot
    {
        [JsonPropertyName("streams")]
        public List<ProbeStream>? Streams { get; set; }

        [JsonPropertyName("format")]
        public ProbeFormat? Format { get; set; }
    }

    private sealed class ProbeFormat
    {
        [JsonPropertyName("format_name")]
        public string? FormatName { get; set; }

        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("size")]
        public string? Size { get; set; }
    }

    private sealed class ProbeStream
    {
        [JsonPropertyName("codec_name")]
        public string? CodecName { get; set; }

        [JsonPropertyName("codec_type")]
        public string? CodecType { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("avg_frame_rate")]
        public string? AvgFrameRate { get; set; }

        [JsonPropertyName("r_frame_rate")]
        public string? RFrameRate { get; set; }

        [JsonPropertyName("nb_frames")]
        public string? NbFrames { get; set; }

        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("channels")]
        public int Channels { get; set; }

        [JsonPropertyName("sample_rate")]
        public string? SampleRate { get; set; }

        [JsonPropertyName("disposition")]
        public ProbeDisposition? Disposition { get; set; }
    }

    private sealed class ProbeDisposition
    {
        [JsonPropertyName("attached_pic")]
        public int AttachedPic { get; set; }
    }
}
