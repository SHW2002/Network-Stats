using NetworkStats.App.Views;

namespace NetworkStats.App.Platforms.Windows;

internal static class WindowTheme
{
    public static void Apply(Microsoft.UI.Xaml.Window window)
    {
        if (window.Content is Microsoft.UI.Xaml.FrameworkElement root)
            root.RequestedTheme = Application.Current?.UserAppTheme switch
            {
                AppTheme.Dark => Microsoft.UI.Xaml.ElementTheme.Dark,
                AppTheme.Light => Microsoft.UI.Xaml.ElementTheme.Light,
                _ => Microsoft.UI.Xaml.ElementTheme.Default
            };
        var title = window.AppWindow.TitleBar;
        title.ForegroundColor = Convert(Ui.Ink.Current);
        title.InactiveForegroundColor = Convert(Ui.Muted.Current);
        title.BackgroundColor = Convert(Ui.Background.Current);
        title.InactiveBackgroundColor = Convert(Ui.Background.Current);
        title.ButtonForegroundColor = Convert(Ui.Ink.Current);
        title.ButtonInactiveForegroundColor = Convert(Ui.Muted.Current);
        title.ButtonBackgroundColor = Convert(Ui.Background.Current);
        title.ButtonInactiveBackgroundColor = Convert(Ui.Background.Current);
        title.ButtonHoverBackgroundColor = Convert(Ui.SecondaryButton.Current);
        title.ButtonPressedBackgroundColor = Convert(Ui.Line.Current);
    }

    private static global::Windows.UI.Color Convert(Color color) =>
        global::Windows.UI.Color.FromArgb((byte)(color.Alpha * 255), (byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255));
}
