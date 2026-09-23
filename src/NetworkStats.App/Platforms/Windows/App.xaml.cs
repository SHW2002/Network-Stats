namespace NetworkStats.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        Platforms.Windows.StartupDiagnostics.Initialize();
        UnhandledException += (_, args) => Platforms.Windows.StartupDiagnostics.Write($"WinUI: {args.Message}\n{args.Exception}");
        // 单文件启动时资源在解包目录，不能按外部 EXE 所在目录查找。
        ResourceManagerRequested += (_, args) => args.CustomResourceManager =
            new Microsoft.Windows.ApplicationModel.Resources.ResourceManager(
                Path.Combine(AppContext.BaseDirectory, typeof(App).Assembly.GetName().Name + ".pri"));
        InitializeComponent();
        Platforms.Windows.StartupDiagnostics.Write("WinUI application initialized");
    }
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            Platforms.Windows.StartupDiagnostics.Write("Launching MAUI window");
            base.OnLaunched(args);
            if (Environment.GetCommandLineArgs().Contains("--startup"))
                ((NetworkStats.App.App)Microsoft.Maui.Controls.Application.Current!).MinimizeForStartup();
            Platforms.Windows.StartupDiagnostics.Write("MAUI launch returned");
            if (Platforms.Windows.PackageVerifier.ReportPath is not null)
            {
                await Platforms.Windows.PackageVerifier.VerifyAsync(Services);
                if (Environment.ExitCode != 0) Exit();
            }
        }
        catch (Exception exception)
        {
            Platforms.Windows.StartupDiagnostics.Write($"Launch failed: {exception}");
            Environment.ExitCode = 1;
            Exit();
        }
    }
}
