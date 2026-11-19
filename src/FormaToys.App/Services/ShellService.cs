using System.Diagnostics;

namespace FormaToys.App.Services;

public static class ShellService
{
    /// <summary>在资源管理器里选中某个文件。</summary>
    public static void RevealFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                return;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
            }
        }
        catch
        {
            // 打不开资源管理器不算错误，忽略。
        }
    }

    public static void OpenFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
            }
        }
        catch
        {
            // 同上。
        }
    }

    /// <summary>用系统默认程序打开一个文件。</summary>
    public static void OpenFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch
        {
            // 没有关联程序时忽略。
        }
    }
}
