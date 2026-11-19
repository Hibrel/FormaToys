using System.Diagnostics;
using FormaToys.Core.Conversion;
using FormaToys.Core.Media;
using FormaToys.Core.Tooling;

namespace FormaToys.Smoke;

/// <summary>
/// 命令行冒烟测试：用本机 ffmpeg 现场生成样本，再把全部目标格式跑一遍，
/// 逐个 ffprobe 校验输出，最后打印结果表。退出码 0 表示全绿。
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var outputDirectory = ParseOutputDirectory(args);
        Directory.CreateDirectory(outputDirectory);

        var toolDirectory = ResolveToolDirectory(args);
        if (toolDirectory is null)
        {
            Console.Error.WriteLine("找不到 ffmpeg 目录。设置 FORMATOYS_FFMPEG_DIR 或把 ffmpeg.exe 放到 assets\\ffmpeg。");
            return 2;
        }

        var tools = ToolPaths.FromDirectory(toolDirectory);
        if (!tools.IsComplete)
        {
            Console.Error.WriteLine($"目录 {toolDirectory} 里缺少 ffmpeg.exe 或 ffprobe.exe。");
            return 2;
        }

        var startup = StartupCheck.Run(tools);
        Console.WriteLine($"ffmpeg: {startup.ShortVersion}  gpl={startup.IsGpl}");
        Console.WriteLine($"工作目录: {outputDirectory}");
        Console.WriteLine();

        var sampleDirectory = Path.Combine(outputDirectory, "samples");
        Directory.CreateDirectory(sampleDirectory);
        var samples = GenerateSamples(tools, sampleDirectory);

        var probe = new MediaProbe(tools);
        var engine = new ConversionEngine(tools);

        Console.WriteLine("== 探测 ==");
        foreach (var sample in samples)
        {
            var info = await probe.ProbeAsync(sample);
            Console.WriteLine(
                $"  {info.FileName,-16} kind={info.Kind,-6} {info.VideoCodec ?? "-"}/{info.AudioCodec ?? "-"} " +
                $"{info.Width}x{info.Height} dur={info.Duration.TotalSeconds:0.##}s {DisplaySize(info.SizeBytes)}");
        }

        Console.WriteLine();
        Console.WriteLine("== 转换 ==");

        var failures = new List<string>();
        var cases = new List<(string Input, string FormatId)>
        {
            (samples[0], "mp4"),
            (samples[0], "mkv"),
            (samples[0], "webm"),
            (samples[0], "gif"),
            (samples[0], "mp3"),
            (samples[0], "wav"),
            (samples[1], "mp3"),
            (samples[1], "m4a"),
            (samples[1], "flac"),
            (samples[1], "ogg"),
            (samples[2], "jpg"),
            (samples[2], "png"),
            (samples[2], "webp"),
            (samples[2], "bmp"),
        };

        foreach (var (input, formatId) in cases)
        {
            var format = FormatCatalog.Find(formatId);
            if (format is null)
            {
                failures.Add($"{formatId}: 格式未在 FormatCatalog 中定义");
                continue;
            }

            var source = await probe.ProbeAsync(input);
            var request = new ConversionRequest
            {
                InputPath = input,
                FormatId = format.Id,
                Quality = QualityLevel.Balanced,
                OutputPath = OutputPlanner.Plan(input, outputDirectory, format.Extension),
                Duration = source.Duration,
                InputKind = source.Kind,
            };

            var sw = Stopwatch.StartNew();
            var progressEvents = 0;
            var determinateEvents = 0;
            var progress = new Progress<ConversionProgress>(value =>
            {
                Interlocked.Increment(ref progressEvents);
                if (value.IsDeterminate)
                {
                    Interlocked.Increment(ref determinateEvents);
                }
            });

            var result = await engine.RunAsync(request, progress, CancellationToken.None);
            sw.Stop();

            if (!result.Succeeded)
            {
                failures.Add($"{Path.GetFileName(input)} -> {format.DisplayName}: {result.ErrorMessage}");
                Console.WriteLine($"  [失败] {Path.GetFileName(input),-14} -> {format.DisplayName,-6} {result.ErrorMessage}");
                continue;
            }

            var produced = await probe.ProbeAsync(result.OutputPath);
            var expectedKind = format.Kind;
            var sizeOk = produced.SizeBytes > 0;
            var kindOk = expectedKind == MediaKind.Image
                ? produced.Kind is MediaKind.Image or MediaKind.Video
                : produced.Kind == expectedKind;

            if (!sizeOk || !kindOk)
            {
                failures.Add($"{format.DisplayName}: 输出校验失败 size={produced.SizeBytes} kind={produced.Kind}");
                Console.WriteLine($"  [校验失败] {format.DisplayName} size={produced.SizeBytes} kind={produced.Kind}");
                continue;
            }

            Console.WriteLine(
                $"  [OK] {Path.GetFileName(input),-14} -> {format.DisplayName,-6} " +
                $"{DisplaySize(produced.SizeBytes),9}  {sw.Elapsed.TotalSeconds,5:0.00}s  " +
                $"进度事件 {progressEvents}（确定态 {determinateEvents}）  {Path.GetFileName(result.OutputPath)}");
        }

        Console.WriteLine();
        if (failures.Count == 0)
        {
            Console.WriteLine($"全部 {cases.Count} 项通过。");
            return 0;
        }

        Console.WriteLine($"{failures.Count} 项失败：");
        foreach (var failure in failures)
        {
            Console.WriteLine("  - " + failure);
        }

        return 1;
    }

    private static List<string> GenerateSamples(ToolPaths tools, string directory)
    {
        var video = Path.Combine(directory, "sample.mp4");
        var audio = Path.Combine(directory, "sample.mp3");
        var image = Path.Combine(directory, "sample.png");

        if (!File.Exists(video))
        {
            RunFfmpeg(tools, "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=30:duration=6",
                "-f", "lavfi", "-i", "sine=frequency=440:duration=6",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-c:a", "aac", "-shortest", video);
        }

        if (!File.Exists(audio))
        {
            RunFfmpeg(tools, "-f", "lavfi", "-i", "sine=frequency=880:duration=5", "-c:a", "libmp3lame", "-q:a", "4", audio);
        }

        if (!File.Exists(image))
        {
            RunFfmpeg(tools, "-f", "lavfi", "-i", "testsrc2=size=800x600:rate=1:duration=1", "-frames:v", "1", image);
        }

        return new List<string> { video, audio, image };
    }

    private static void RunFfmpeg(ToolPaths tools, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(tools.FfmpegPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("生成样本失败: " + error);
        }
    }

    private static string? ResolveToolDirectory(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--ffmpeg-dir" or "-f")
            {
                var candidate = Path.GetFullPath(args[i + 1]);
                return Directory.Exists(candidate) ? candidate : null;
            }
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("FORMATOYS_FFMPEG_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && Directory.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "assets", "ffmpeg");
            if (File.Exists(Path.Combine(candidate, "ffmpeg.exe")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string ParseOutputDirectory(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--out" or "-o")
            {
                return Path.GetFullPath(args[i + 1]);
            }
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "global.json")))
        {
            current = current.Parent;
        }

        var root = current?.FullName ?? Directory.GetCurrentDirectory();
        return Path.Combine(root, "artifacts", "smoke");
    }

    private static string DisplaySize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024d / 1024d:0.0} MB",
        >= 1024 => $"{bytes / 1024d:0.0} KB",
        _ => $"{bytes} B",
    };
}
