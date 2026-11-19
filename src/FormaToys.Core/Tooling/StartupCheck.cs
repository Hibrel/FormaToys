using System.Diagnostics;

namespace FormaToys.Core.Tooling;

public sealed record FfmpegInfo(string Version, string Configuration, bool IsGpl, string ExecutablePath)
{
    public string ShortVersion
    {
        get
        {
            var token = Version.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return token ?? "unknown";
        }
    }
}

/// <summary>启动自检：ffmpeg 能否跑起来、是否 GPL 版本、有没有 ffprobe。</summary>
public static class StartupCheck
{
    public static FfmpegInfo Run(ToolPaths tools)
    {
        var psi = new ProcessStartInfo(tools.FfmpegPath)
        {
            WorkingDirectory = tools.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-version");

        using var process = Process.Start(psi)
                            ?? throw new ToolNotFoundException("无法启动 ffmpeg.exe。");

        var stdout = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(15000);

        var lines = stdout.Split('\n', StringSplitOptions.TrimEntries);
        var versionLine = lines.FirstOrDefault(l => l.StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase))
                          ?? lines.FirstOrDefault()
                          ?? string.Empty;
        var configLine = lines.FirstOrDefault(l => l.StartsWith("configuration:", StringComparison.OrdinalIgnoreCase))
                         ?? string.Empty;

        var version = versionLine.Replace("ffmpeg version", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var isGpl = configLine.Contains("--enable-gpl", StringComparison.OrdinalIgnoreCase);

        return new FfmpegInfo(version, configLine, isGpl, tools.FfmpegPath);
    }
}
