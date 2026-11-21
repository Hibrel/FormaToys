using System.IO.Compression;
using System.Reflection;

namespace FormaToys.Core.Tooling;

/// <summary>
/// 随程序集携带的 ffmpeg / ffprobe（Brotli 压缩的嵌入资源）。
/// 首次使用时解压到 %LOCALAPPDATA%\FormaToys\tools\&lt;key&gt;，之后直接复用。
/// 资源由 tools\pack-ffmpeg.ps1 生成；没有资源时（例如本地开发直接放 assets）返回 null。
/// </summary>
public static class EmbeddedTools
{
    public const string FfmpegResource = "FormaToys.Tools.ffmpeg.exe.br";
    public const string FfprobeResource = "FormaToys.Tools.ffprobe.exe.br";
    public const string VersionResource = "FormaToys.Tools.ffmpeg-version.txt";

    private static readonly object Gate = new();
    private static string? _resolvedDirectory;
    private static bool _attempted;

    /// <summary>嵌入资源是否存在（打包时是否带上了 ffmpeg）。</summary>
    public static bool IsAvailable => FindResource(FfmpegResource) is not null && FindResource(FfprobeResource) is not null;

    /// <summary>嵌在程序集里的 ffmpeg 版本串，没有资源时返回 null。</summary>
    public static string? EmbeddedVersion
    {
        get
        {
            try
            {
                using var stream = FindResource(VersionResource);
                if (stream is null)
                {
                    return null;
                }

                using var reader = new StreamReader(stream);
                var line = reader.ReadLine();
                return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 解压（或复用已解压的）ffmpeg 目录；失败或没有嵌入资源时返回 null，调用方继续走环境变量 / PATH。
    /// </summary>
    public static string? EnsureExtracted()
    {
        lock (Gate)
        {
            if (_attempted)
            {
                return _resolvedDirectory;
            }

            _attempted = true;
            try
            {
                if (!IsAvailable)
                {
                    return null;
                }

                var directory = Path.Combine(CacheRoot, CacheKey());
                var ffmpeg = Path.Combine(directory, ToolPaths.FfmpegFileName);
                var ffprobe = Path.Combine(directory, ToolPaths.FfprobeFileName);

                if (File.Exists(ffmpeg) && File.Exists(ffprobe) && File.Exists(Path.Combine(directory, ".complete")))
                {
                    _resolvedDirectory = directory;
                    return directory;
                }

                Directory.CreateDirectory(directory);
                Extract(FfmpegResource, ffmpeg);
                Extract(FfprobeResource, ffprobe);
                File.WriteAllText(Path.Combine(directory, ".complete"), EmbeddedVersion ?? "unknown");
                _resolvedDirectory = directory;
                return directory;
            }
            catch
            {
                _resolvedDirectory = null;
                return null;
            }
        }
    }

    private static string CacheRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FormaToys",
        "tools");

    /// <summary>缓存目录按“程序集版本 + 资源长度”区分，换版本就换目录，旧目录可直接删。</summary>
    private static string CacheKey()
    {
        var version = typeof(EmbeddedTools).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var length = (FindResource(FfmpegResource)?.Length ?? 0) + (FindResource(FfprobeResource)?.Length ?? 0);
        return $"{version}-{length:x}";
    }

    private static void Extract(string resourceName, string destination)
    {
        using var source = FindResource(resourceName)
            ?? throw new InvalidOperationException($"缺少嵌入资源 {resourceName}");

        var temp = destination + ".tmp";
        using (var brotli = new BrotliStream(source, CompressionMode.Decompress))
        using (var output = File.Create(temp))
        {
            brotli.CopyTo(output, 1 << 20);
        }

        File.Move(temp, destination, overwrite: true);
    }

    private static Stream? FindResource(string name) =>
        typeof(EmbeddedTools).Assembly.GetManifestResourceStream(name);
}
