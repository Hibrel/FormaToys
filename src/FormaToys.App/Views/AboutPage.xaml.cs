using FormaToys.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FormaToys.App.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
    }

    private static readonly string AppVersion =
        typeof(AboutPage).Assembly.GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";

    public string VersionText => $"版本 {AppVersion}";

    private void OnOpenLicensesClick(object sender, RoutedEventArgs e)
    {
        var license = Path.Combine(AppContext.BaseDirectory, "LICENSE.txt");
        if (File.Exists(license))
        {
            ShellService.OpenFile(license);
            return;
        }

        ShellService.OpenFolder(AppContext.BaseDirectory);
    }
}
