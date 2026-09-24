using NetworkStats.App.Startup;

namespace NetworkStats.App.Views;

internal sealed class StartupSettingsView : ContentView
{
    private readonly Switch _enabled = new();
    private readonly Label _detail = Ui.Text("", 11, Ui.Muted);
    internal bool Enabled { get => _enabled.IsToggled; set => _enabled.IsToggled = value; }
    internal StartupStatus Status { get; private set; }

    public StartupSettingsView(StartupStatus status)
    {
        Status = status;
        Enabled = status.Registered;
        _enabled.IsEnabled = status.Supported;
        _detail.Text = status.Detail;
        Ui.Bind(_enabled, Switch.OnColorProperty, Ui.PrimaryButton);
        var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        row.Add(Ui.Text("开机自启动", 13, bold: true));
        row.Add(_enabled, 1);
        var settings = Ui.Button("系统启动设置");
        settings.IsEnabled = status.Supported;
        settings.Clicked += async (_, _) =>
        {
            try { await StartupServices.OpenSystemSettingsAsync(); }
            catch (Exception exception) { _detail.Text = "无法打开系统设置：" + exception.Message; }
        };
        Content = Ui.Card(new VerticalStackLayout { Spacing = Ui.Space(10), Children =
            { Ui.Text("启动行为", 17, bold: true), row, _detail, settings } });
#if !WINDOWS && !MACCATALYST
        IsVisible = false;
#endif
    }

    public void ShowStatus(StartupStatus status) { Status = status; _detail.Text = status.Detail; }
}
