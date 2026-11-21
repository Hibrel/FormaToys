using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FormaToys.Setup;

/// <summary>
/// 单文件启动器：把内嵌的 App 载荷解压到 %LOCALAPPDATA%\FormaToys\app\&lt;版本&gt;，然后启动 FormaToys.exe。
/// 版本目录已存在且带 .complete 标记时跳过解压，直接启动。
/// </summary>
internal static class Program
{
    private const string PayloadResource = "FormaToys.Setup.payload.zip";
    private const string AppExecutable = "FormaToys.exe";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    [STAThread]
    private static int Main()
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FormaToys",
                "app",
                version);

            var exe = Path.Combine(root, AppExecutable);
            var marker = Path.Combine(root, ".complete");

            if (!File.Exists(marker) || !File.Exists(exe))
            {
                Extract(root, marker);
            }

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                WorkingDirectory = root,
            };

            foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
            {
                psi.ArgumentList.Add(arg);
            }

            Process.Start(psi);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBoxW(IntPtr.Zero, "启动失败：" + ex.Message, "FormaToys", 0x10);
            return 1;
        }
    }

    private static void Extract(string root, string marker)
    {
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource)
            ?? throw new InvalidOperationException("安装包缺少应用载荷，请重新下载。");

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(root);
        ZipFile.ExtractToDirectory(payload, root, overwriteFiles: true);
        File.WriteAllText(marker, Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0");
    }
}
