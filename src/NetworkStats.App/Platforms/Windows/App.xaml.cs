namespace NetworkStats.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        RequestedTheme = Microsoft.UI.Xaml.ApplicationTheme.Light;
        InitializeComponent();
    }
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
