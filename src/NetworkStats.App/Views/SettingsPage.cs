using NetworkStats.Configuration;
using NetworkStats.Models;
using NetworkStats.Monitoring;
using NetworkStats.App.Startup;

namespace NetworkStats.App.Views;

internal sealed class SettingsPage : ContentPage
{
    private readonly MonitorEngine _monitor;
    private readonly MonitorSettings _initial;
    internal AppearanceSettingsView Appearance { get; }
    internal StartupSettingsView Startup { get; }
    internal SpeedMeasurementSettingsView SpeedMeasurement { get; }
    internal RouteSpeedSettingsView DirectSpeedMeasurement { get; }
    private readonly IStartupService _startup;
    private readonly Entry _interval;
    private readonly Entry _timeout;
    private readonly Entry _threshold;
    private readonly Entry _retention;
    private readonly VerticalStackLayout _sites = new() { Spacing = 10 };
    private readonly VerticalStackLayout _proxies = new() { Spacing = 10 };
    private readonly Label _error = Ui.Text("", 13, Ui.Red);
    private readonly Button _save = Ui.Button("保存设置", true);

    public SettingsPage(MonitorEngine monitor, IStartupService? startup = null)
    {
        _monitor = monitor;
        _initial = monitor.Settings;
        Title = "探测设置";
        Ui.Bind(this, BackgroundColorProperty, Ui.Background);
        Appearance = new(_initial);
        SpeedMeasurement = new(_initial.SpeedMeasurementEnabled);
        DirectSpeedMeasurement = new("直连启用周期网速检测", _initial.DirectSpeedMeasurementEnabled,
            "全局开关开启后，直连线路才会读取响应体并计算下载速度。");
        DirectSpeedMeasurement.Changed += (_, _) => RefreshTrafficEstimate();
        _startup = startup ?? StartupServices.Current;
        try { Startup = new(_startup.Read()); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { Startup = new(new(false, _initial.LaunchOnStartup, "无法读取系统启动项：" + exception.Message)); }
        _interval = Ui.Input(_initial.IntervalSeconds.ToString(), "60", Keyboard.Numeric);
        _timeout = Ui.Input(_initial.TimeoutSeconds.ToString(), "10", Keyboard.Numeric);
        _threshold = Ui.Input(_initial.SlowThresholdMs.ToString(), "1500", Keyboard.Numeric);
        _retention = Ui.Input(_initial.RetentionHours.ToString(), "168", Keyboard.Numeric);
        _interval.TextChanged += (_, _) => RefreshTrafficEstimate();
        foreach (var site in _initial.Sites) AddSite(site);
        foreach (var proxy in _initial.Proxies) AddProxy(proxy);
        var addSite = Ui.Button("+ 添加网站");
        addSite.Clicked += (_, _) => AddSite(new("", "https://"));
        var addProxy = Ui.Button("+ 添加代理");
        addProxy.Clicked += (_, _) => AddProxy(new($"代理 {_proxies.Children.Count + 1}", "http", "127.0.0.1", 7890));
        _save.Clicked += async (_, _) => await SaveAsync();
        var updates = Ui.Button("检查更新");
        updates.Clicked += async (_, _) => await OpenUpdatesAsync();
        var parameters = new VerticalStackLayout { Spacing = Ui.Space(12) };
        parameters.Add(Field("探测间隔 / 秒", "10–3600，默认 60；图表按每次已完成探测显示一格", _interval));
        parameters.Add(Field("请求超时 / 秒", "1–60，超时显示红色", _timeout));
        parameters.Add(Field("黄色阈值 / 毫秒", "成功请求超过此耗时显示黄色，需小于超时", _threshold));
        parameters.Add(Field("历史保留 / 小时", "1–168，默认保留 7 天", _retention));
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = Ui.Space(18), Padding = Ui.Space(22), MaximumWidthRequest = 850,
                Children =
                {
                    Ui.Text("配置仅对这台设备生效", 21, bold: true),
                    Ui.Text("保存后自动安排一轮探测；直连始终保留且不会使用系统 HTTP / SOCKS 代理。", 12, Ui.Muted),
                    Ui.Card(new VerticalStackLayout { Spacing = Ui.Space(8), Children =
                        { Ui.Text("软件更新", 17, bold: true), Ui.Text($"当前版本 v{AppInfo.Current.VersionString} · GitHub Releases · 可配置更新代理", 12, Ui.Muted), updates } }),
                    Appearance, Startup, SpeedMeasurement,
                    Ui.Text("线路测速开关", 17, bold: true), DirectSpeedMeasurement, Ui.Card(parameters),
                    Ui.Text("目标网站", 17, bold: true), _sites, addSite,
                    Ui.Text("代理线路", 17, bold: true),
                    Ui.Text("127.0.0.1 指当前设备。手机连接电脑上的代理时，请填写电脑的局域网 IP，并允许代理接受局域网连接。", 12, Ui.Muted),
                    _proxies, addProxy, _error, _save,
                    Ui.Text($"数据保存在此设备：\n{AppStorage.DataDirectory}", 11, Ui.Muted)
                }
            }
        };
        RefreshTrafficEstimate();
    }

    private static View Field(string title, string note, Entry input) => new VerticalStackLayout
    {
        Spacing = 5,
        Children = { Ui.Text(title, 13, bold: true), Ui.Text(note, 11, Ui.Muted), input }
    };

    private void AddSite(SiteDefinition site)
    {
        _sites.Add(new SiteEditor(site, editor => { _sites.Remove(editor); RefreshTrafficEstimate(); }));
        RefreshTrafficEstimate();
    }

    private void AddProxy(ProxyDefinition proxy)
    {
        var editor = new ProxyEditor(proxy, removed => { _proxies.Remove(removed); RefreshTrafficEstimate(); });
        editor.Changed += (_, _) => RefreshTrafficEstimate();
        _proxies.Add(editor);
        RefreshTrafficEstimate();
    }

    private void RefreshTrafficEstimate() => SpeedMeasurement.UpdateEstimate(
        Number(_interval), _sites.Children.Count,
        (DirectSpeedMeasurement.Enabled ? 1 : 0) + _proxies.Children.OfType<ProxyEditor>().Count(editor => editor.SpeedMeasurementEnabled));

    internal async Task<UpdatePage> OpenUpdatesAsync(Updates.IUpdateInstaller? installer = null, Func<string?, HttpClient>? clientFactory = null)
    {
        var page = new UpdatePage(_monitor, installer, clientFactory);
        await Navigation.PushAsync(page, false);
        return page;
    }

    internal async Task SaveAsync()
    {
        _save.IsEnabled = false;
        try
        {
            var updated = _initial with
            {
                UpdateProxy = _monitor.Settings.UpdateProxy,
                Theme = Appearance.SelectedTheme, MinimizeOnClose = Appearance.MinimizeOnClose,
                LaunchOnStartup = Startup.Status.Supported ? Startup.Enabled : _initial.LaunchOnStartup,
                SpeedMeasurementEnabled = SpeedMeasurement.Enabled,
                DirectSpeedMeasurementEnabled = DirectSpeedMeasurement.Enabled,
                IntervalSeconds = Number(_interval), TimeoutSeconds = Number(_timeout), SlowThresholdMs = Number(_threshold),
                RetentionHours = Number(_retention),
                Sites = _sites.Children.OfType<SiteEditor>().Select(editor => editor.Read()).ToArray(),
                Proxies = _proxies.Children.OfType<ProxyEditor>().Select(editor => editor.Read()).ToArray()
            };
            updated = SettingsValidator.Normalize(updated);
            var previousStartup = Startup.Status.Supported ? _startup.Read() : Startup.Status;
            var changeStartup = previousStartup.Supported &&
                (previousStartup.Registered != Startup.Enabled || (Startup.Enabled && previousStartup.NeedsUpdate));
            if (changeStartup) _startup.SetEnabled(Startup.Enabled);
            try { await _monitor.SaveSettingsAsync(updated); }
            catch
            {
                if (changeStartup) _startup.SetEnabled(previousStartup.Registered);
                throw;
            }
            var currentStartup = Startup.Status.Supported ? _startup.Read() : Startup.Status;
            Startup.ShowStatus(currentStartup);
            if (currentStartup.RequiresApproval) { _error.Text = currentStartup.Detail; return; }
            await Navigation.PopAsync();
        }
        catch (SettingsValidationException exception) { _error.Text = string.Join("\n", exception.Errors); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        { _error.Text = $"配置保存失败：{exception.Message}"; }
        finally { _save.IsEnabled = true; }
    }

    private static int Number(Entry entry) => int.TryParse(entry.Text, out var value) ? value : 0;
}
