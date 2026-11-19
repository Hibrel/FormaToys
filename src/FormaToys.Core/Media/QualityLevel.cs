namespace FormaToys.Core.Media;

/// <summary>全局质量档位：界面上的“文件小 / 均衡 / 高画质”三段。</summary>
public enum QualityLevel
{
    Small = 0,
    Balanced = 1,
    High = 2,
}

public static class QualityLevelExtensions
{
    public static QualityLevel FromIndex(int index) => index switch
    {
        <= 0 => QualityLevel.Small,
        1 => QualityLevel.Balanced,
        _ => QualityLevel.High,
    };

    public static int ToIndex(this QualityLevel level) => (int)level;
}
