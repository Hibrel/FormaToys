using System.Text;
using FormaToys.App.Services;
using Microsoft.UI.Xaml;

namespace FormaToys.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    /// <summary>启动时通过命令行传进来的文件（“发送到”/资源管理器打开方式）。</summary>
    public static IReadOnlyList<string> StartupFiles { get; private set; } = Array.Empty<string>();

    public static Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartupFiles = Environment.GetCommandLineArgs()
            .Skip(1)
            .Where(a => !a.StartsWith('-') && File.Exists(a))
            .ToArray();

        AppServices.Initialize();

        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FormaToys");
            Directory.CreateDirectory(dir);
            var builder = new StringBuilder()
                .AppendLine($"[{DateTime.Now:O}] {e.Message}")
                .AppendLine(e.Exception?.ToString());
            File.AppendAllText(Path.Combine(dir, "crash.log"), builder.ToString());
        }
        catch
        {
            // 记录崩溃日志本身不该再崩一次。
        }
    }
}
