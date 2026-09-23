using NetworkStats.Configuration;
using NetworkStats.Models;
using NetworkStats.Monitoring;
using NetworkStats.Probing;
using NetworkStats.Storage;
using NetworkStats.App.Views;

namespace NetworkStats.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddSingleton(_ => new SettingsStore(AppStorage.DataDirectory, new MonitorSettings()));
        builder.Services.AddSingleton(_ => new HistoryStore(AppStorage.DataDirectory));
        builder.Services.AddSingleton<WebsiteProbe>();
        builder.Services.AddSingleton<MonitorEngine>();
        builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}
