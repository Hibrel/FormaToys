namespace FormaToys.Core.Tooling;

public sealed class ToolNotFoundException : Exception
{
    public ToolNotFoundException(string message) : base(message)
    {
    }
}

/// <summary>
/// 定位随包附带的 ffmpeg / ffprobe。优先级：
/// 1) 程序目录下的 ffmpeg\ 子目录（本地开发 / 手工放盘）
/// 2) 程序目录本身
/// 3) 程序集内嵌的 ffmpeg（首次使用时解压到 %LOCALAPPDATA%）
/// 4) 环境变量 FORMATOYS_FFMPEG_DIR
/// 5) PATH
/// </summary>
public sealed class ToolPaths
{
    public const string FfmpegFileName = "ffmpeg.exe";
    public const string FfprobeFileName = "ffprobe.exe";
    public const string EnvironmentVariable = "FORMATOYS_FFMPEG_DIR";

    private ToolPaths(string directory)
    {
        WorkingDirectory = directory;
        FfmpegPath = Path.Combine(directory, FfmpegFileName);
        FfprobePath = Path.Combine(directory, FfprobeFileName);
    }

    public string WorkingDirectory { get; }

    public string FfmpegPath { get; }

    public string FfprobePath { get; }

    public bool IsComplete => File.Exists(FfmpegPath) && File.Exists(FfprobePath);

    public static ToolPaths FromDirectory(string directory) => new(Path.GetFullPath(directory));

    public static ToolPaths Resolve(string? baseDirectory = null)
    {
        var appDir = baseDirectory ?? AppContext.BaseDirectory;

        foreach (var candidate in Candidates(appDir))
        {
            if (File.Exists(Path.Combine(candidate, FfmpegFileName)))
            {
                return new ToolPaths(Path.GetFullPath(candidate));
            }
        }

        var embedded = EmbeddedTools.EnsureExtracted();
        if (!string.IsNullOrEmpty(embedded) && File.Exists(Path.Combine(embedded, FfmpegFileName)))
        {
            return new ToolPaths(embedded);
        }

        throw new ToolNotFoundException(
            "没有找到 ffmpeg.exe / ffprobe.exe。请把它们放到程序目录的 ffmpeg\\ 子目录，或设置环境变量 " +
            EnvironmentVariable + " 指向所在目录。");
    }

    private static IEnumerable<string> Candidates(string appDir)
    {
        yield return Path.Combine(appDir, "ffmpeg");
        yield return appDir;

        var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            yield return fromEnv;
        }

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(dir))
            {
                yield return dir;
            }
        }
    }
}
