using FormaToys.Core.Media;

namespace FormaToys.Core.Conversion;

/// <summary>决定输出文件路径：同目录或自定义目录，重名则追加 " (1)"、" (2)"…</summary>
public static class OutputPlanner
{
    public static string Plan(string inputPath, string? outputDirectory, string extension)
    {
        var sourceDir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var targetDir = System.IO.Path.GetFullPath(string.IsNullOrWhiteSpace(outputDirectory) ? sourceDir : outputDirectory);

        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "output";
        }

        var ext = extension.StartsWith('.') ? extension : "." + extension;

        Directory.CreateDirectory(targetDir);

        var candidate = Path.Combine(targetDir, baseName + ext);
        var index = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(targetDir, $"{baseName} ({index}){ext}");
            index++;
        }

        return candidate;
    }

    public static string? DirectoryOf(string? folder) =>
        string.IsNullOrWhiteSpace(folder) ? null : folder;
}
