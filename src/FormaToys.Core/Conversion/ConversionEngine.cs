using System.Diagnostics;

namespace FormaToys.Core.Conversion;

/// <summary>跑一次 ffmpeg：启进程、喂参数、读进度、收错误尾巴、支持取消。</summary>
public sealed class ConversionEngine
{
    /// <summary>进度回读节奏（毫秒）。ffmpeg 每 0.5s 吐一行 -progress，这里按 16ms 采样最新值。</summary>
    public const int ProgressIntervalMs = 16;

    private const int ErrorTailLines = 30;

    private readonly Tooling.ToolPaths _tools;

    public ConversionEngine(Tooling.ToolPaths tools) => _tools = tools;

    public async Task<ConversionResult> RunAsync(
        ConversionRequest request,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // ffmpeg 的工作目录是工具目录，所有相对路径必须先固化成绝对路径，否则会解析到错误位置。
        var absoluteRequest = new ConversionRequest
        {
            InputPath = System.IO.Path.GetFullPath(request.InputPath),
            OutputPath = System.IO.Path.GetFullPath(request.OutputPath),
            FormatId = request.FormatId,
            Quality = request.Quality,
            Duration = request.Duration,
            InputKind = request.InputKind,
        };
        var arguments = FfmpegCommandBuilder.Build(absoluteRequest);

        var psi = new ProcessStartInfo(_tools.FfmpegPath)
        {
            WorkingDirectory = _tools.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        var errorTail = new Queue<string>();
        var errorLock = new object();

        // ffmpeg 的 -progress 输出直接写 stdout，这里只保存“最新一条”，
        // 由 16ms 的采样器统一对外报告，避免 UI 线程被高频更新淹没。
        long latestTicks = 0;
        var finished = 0;

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            var line = e.Data;
            if (line is null)
            {
                return;
            }

            if (ProgressParser.TryParseTimestamp(line, out var elapsed))
            {
                Interlocked.Exchange(ref latestTicks, elapsed.Ticks);
            }
            else if (ProgressParser.IsFinished(line))
            {
                Interlocked.Exchange(ref finished, 1);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            var line = e.Data;
            if (line is null)
            {
                return;
            }

            lock (errorLock)
            {
                errorTail.Enqueue(line);
                while (errorTail.Count > ErrorTailLines)
                {
                    errorTail.Dequeue();
                }
            }
        };

        try
        {
            if (!process.Start())
            {
                return ConversionResult.Failure("无法启动 ffmpeg 进程。", stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            return ConversionResult.Failure($"无法启动 ffmpeg：{ex.Message}", stopwatch.Elapsed);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var total = absoluteRequest.Duration;
        var determinate = total > TimeSpan.FromMilliseconds(50);

        Timer? ticker = null;
        if (progress is not null)
        {
            ticker = new Timer(
                _ =>
                {
                    var elapsed = TimeSpan.FromTicks(Interlocked.Read(ref latestTicks));
                    var done = Volatile.Read(ref finished) == 1;
                    var ratio = done
                        ? 1d
                        : determinate
                            ? Math.Clamp(elapsed.TotalSeconds / total.TotalSeconds, 0d, 0.995d)
                            : 0d;

                    progress.Report(new ConversionProgress(ratio, elapsed, total));
                },
                null,
                ProgressIntervalMs,
                ProgressIntervalMs);
        }

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // 进程已经没了，忽略。
            }
        });

        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ConversionResult.Failure(ex.Message, stopwatch.Elapsed);
        }
        finally
        {
            if (ticker is not null)
            {
                await ticker.DisposeAsync().ConfigureAwait(false);
            }
        }

        stopwatch.Stop();

        if (cancellationToken.IsCancellationRequested)
        {
            TryDelete(absoluteRequest.OutputPath);
            return ConversionResult.Cancelled(stopwatch.Elapsed);
        }

        if (process.ExitCode != 0)
        {
            string message;
            lock (errorLock)
            {
                message = errorTail.Count > 0
                    ? string.Join(Environment.NewLine, errorTail)
                    : $"ffmpeg 退出码 {process.ExitCode}。";
            }

            TryDelete(absoluteRequest.OutputPath);
            return ConversionResult.Failure(message, stopwatch.Elapsed);
        }

        var produced = new FileInfo(absoluteRequest.OutputPath);
        if (!produced.Exists || produced.Length == 0)
        {
            return ConversionResult.Failure("ffmpeg 正常退出，但没有生成输出文件。", stopwatch.Elapsed);
        }

        progress?.Report(new ConversionProgress(1d, determinate ? total : stopwatch.Elapsed, total));
        return ConversionResult.Success(absoluteRequest.OutputPath, stopwatch.Elapsed);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 删不掉就算了，别让进度条卡在错误信息上。
        }
    }
}
