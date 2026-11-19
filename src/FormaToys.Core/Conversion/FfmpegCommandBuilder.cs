using FormaToys.Core.Media;

namespace FormaToys.Core.Conversion;

/// <summary>
/// 把“目标格式 + 质量档”翻译成 ffmpeg 参数。
/// 原则：只用各平台通用的编码器组合，输出优先保证“到处都能播”。
/// </summary>
public static class FfmpegCommandBuilder
{
    public static IReadOnlyList<string> Build(ConversionRequest request)
    {
        var format = FormatCatalog.Find(request.FormatId)
                     ?? throw new ArgumentException($"未知的目标格式：{request.FormatId}", nameof(request));

        var args = new List<string>
        {
            "-hide_banner",
            "-nostdin",
            "-y",
            "-loglevel", "error",
            "-progress", "pipe:1",
            "-stats_period", "0.5",
            "-i", request.InputPath,
        };

        switch (format.Id)
        {
            case "mp4":
                AppendH264(args, request.Quality, "medium");
                args.AddRange(new[] { "-c:a", "aac", "-b:a", AudioBitrateKbps(request.Quality) + "k" });
                args.AddRange(new[] { "-pix_fmt", "yuv420p", "-movflags", "+faststart" });
                break;

            case "mkv":
                AppendH264(args, request.Quality, "medium");
                args.AddRange(new[] { "-c:a", "aac", "-b:a", AudioBitrateKbps(request.Quality) + "k" });
                break;

            case "webm":
                AppendVp9(args, request.Quality);
                args.AddRange(new[] { "-c:a", "libopus", "-b:a", request.Quality == QualityLevel.High ? "192k" : request.Quality == QualityLevel.Small ? "96k" : "128k" });
                args.AddRange(new[] { "-row-mt", "1", "-deadline", "good" });
                break;

            case "gif":
                AppendGif(args, request.Quality);
                break;

            case "mp3":
                args.AddRange(new[] { "-vn", "-c:a", "libmp3lame", "-q:a", request.Quality switch { QualityLevel.Small => "7", QualityLevel.High => "0", _ => "2" } });
                break;

            case "m4a":
                args.AddRange(new[] { "-vn", "-c:a", "aac", "-b:a", AudioBitrateKbps(request.Quality) + "k" });
                break;

            case "wav":
                args.AddRange(new[] { "-vn", "-c:a", "pcm_s16le" });
                break;

            case "flac":
                args.AddRange(new[] { "-vn", "-c:a", "flac", "-compression_level", "8" });
                break;

            case "ogg":
                args.AddRange(new[] { "-vn", "-c:a", "libvorbis", "-q:a", request.Quality switch { QualityLevel.Small => "3", QualityLevel.High => "8", _ => "5" } });
                break;

            case "jpg":
                args.AddRange(new[] { "-frames:v", "1", "-c:v", "mjpeg", "-q:v", request.Quality switch { QualityLevel.Small => "9", QualityLevel.High => "2", _ => "4" } });
                break;

            case "png":
                args.AddRange(new[] { "-frames:v", "1", "-c:v", "png", "-compression_level", "9" });
                break;

            case "webp":
                args.AddRange(new[] { "-frames:v", "1", "-c:v", "libwebp", "-quality", request.Quality switch { QualityLevel.Small => "70", QualityLevel.High => "95", _ => "85" }, "-compression_level", "6" });
                break;

            case "bmp":
                args.AddRange(new[] { "-frames:v", "1", "-c:v", "bmp" });
                break;

            default:
                throw new ArgumentException($"格式 {format.Id} 还没有参数模板。", nameof(request));
        }

        args.Add(request.OutputPath);
        return args;
    }

    private static void AppendH264(List<string> args, QualityLevel quality, string preset)
    {
        var (crf, speed) = quality switch
        {
            QualityLevel.Small => ("30", "veryfast"),
            QualityLevel.High => ("18", "slow"),
            _ => ("23", preset),
        };

        args.AddRange(new[]
        {
            "-map", "0:v:0", "-map", "0:a:0?",
            "-c:v", "libx264",
            "-preset", speed,
            "-crf", crf,
            "-profile:v", "high",
            "-level", "4.1",
        });
    }

    private static void AppendVp9(List<string> args, QualityLevel quality)
    {
        var (crf, cpuUsed) = quality switch
        {
            QualityLevel.Small => ("36", "5"),
            QualityLevel.High => ("28", "1"),
            _ => ("32", "3"),
        };

        args.AddRange(new[]
        {
            "-map", "0:v:0", "-map", "0:a:0?",
            "-c:v", "libvpx-vp9",
            "-crf", crf,
            "-b:v", "0",
            "-cpu-used", cpuUsed,
        });
    }

    private static void AppendGif(List<string> args, QualityLevel quality)
    {
        var (fps, width, dither) = quality switch
        {
            QualityLevel.Small => ("12", "360", "none"),
            QualityLevel.High => ("24", "640", "sierra2_4a"),
            _ => ("15", "480", "bayer:bayer_scale=4"),
        };

        var filter =
            $"fps={fps},scale={width}:-2:flags=lanczos,split[s0][s1];" +
            $"[s0]palettegen=max_colors=256:stats_mode=diff[p];" +
            $"[s1][p]paletteuse=dither={dither}";

        args.AddRange(new[] { "-an", "-filter_complex", filter, "-loop", "0" });
    }

    private static string AudioBitrateKbps(QualityLevel quality) => quality switch
    {
        QualityLevel.Small => "96",
        QualityLevel.High => "256",
        _ => "160",
    };
}
