using FormaToys.Core.Media;

namespace FormaToys.Core.Conversion;

/// <summary>一次转换的完整输入：源文件、目标格式、质量档、输出路径。</summary>
public sealed class ConversionRequest
{
    public required string InputPath { get; init; }

    public required string FormatId { get; init; }

    public QualityLevel Quality { get; init; } = QualityLevel.Balanced;

    public required string OutputPath { get; init; }

    /// <summary>源文件时长，用来把 ffmpeg 的进度换算成百分比；未知时进度按不确定处理。</summary>
    public TimeSpan Duration { get; init; }

    public MediaKind InputKind { get; init; } = MediaKind.Unknown;
}

public sealed class ConversionResult
{
    public bool Succeeded { get; init; }

    public bool Canceled { get; init; }

    public string? ErrorMessage { get; init; }

    public TimeSpan Elapsed { get; init; }

    public string OutputPath { get; init; } = string.Empty;

    public static ConversionResult Success(string outputPath, TimeSpan elapsed) =>
        new() { Succeeded = true, OutputPath = outputPath, Elapsed = elapsed };

    public static ConversionResult Failure(string message, TimeSpan elapsed) =>
        new() { ErrorMessage = message, Elapsed = elapsed };

    public static ConversionResult Cancelled(TimeSpan elapsed) =>
        new() { Canceled = true, Elapsed = elapsed };
}
