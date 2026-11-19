using FormaToys.App.Views;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FormaToys.App;

public sealed partial class MainWindow : Window
{
    private bool _navigating;

    public MainWindow()
    {
        InitializeComponent();

        Title = "FormaToys";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }
        catch
        {
            // 老机器 / 关闭特效时会失败，退回普通背景。
        }

        Current = this;

        Nav.SelectedItem = ConvertNavItem;
        NavigateTo("convert");

        SizeWindow(1280, 820);
        ApplyWindowIcon();
    }

    public static new MainWindow? Current { get; private set; }

    private void ApplyWindowIcon()
    {
        try
        {
            var icon = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(icon) && File.Exists(icon))
            {
                AppWindow.SetIcon(icon);
            }
        }
        catch
        {
            // 图标设置失败不影响使用。
        }
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_navigating || args.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        NavigateTo(tag);
    }

    /// <summary>按侧栏标签切换页面。</summary>
    public void NavigateTo(string tag)
    {
        var page = tag switch
        {
            "settings" => typeof(SettingsPage),
            "about" => typeof(AboutPage),
            _ => typeof(ConvertPage),
        };

        _navigating = true;
        try
        {
            Nav.SelectedItem = tag switch
            {
                "settings" => SettingsNavItem,
                "about" => AboutNavItem,
                _ => ConvertNavItem,
            };

            if (ContentFrame.CurrentSourcePageType != page)
            {
                ContentFrame.Navigate(page);
            }
        }
        finally
        {
            _navigating = false;
        }
    }

    private void SizeWindow(int width, int height)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var dpi = GetDpiForWindow(hwnd);
            var scale = dpi <= 0 ? 1d : dpi / 96d;
            AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(width * scale), (int)(height * scale)));
        }
        catch
        {
            // 尺寸调整失败不影响使用。
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
