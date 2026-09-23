using NetworkStats.Configuration;
using NetworkStats.Models;
using NetworkStats.Monitoring;

namespace NetworkStats.App.Views;

internal sealed class SettingsPage : ContentPage
{
    private readonly MonitorEngine _monitor;
    private readonly MonitorSettings _initial;
    private readonly Entry _interval;
    private readonly Entry _timeout;
    private readonly Entry _threshold;
    private readonly Entry _retention;
    private readonly VerticalStackLayout _sites = new() { Spacing = 10 };
    private readonly VerticalStackLayout _proxies = new() { Spacing = 10 };
    private readonly Label _error = Ui.Text("", 13, Ui.Red);
    private readonly Button _save = Ui.Button("保存设置", true);

    public SettingsPage(MonitorEngine monitor)
    {
        _monitor = monitor;
        _initial = monitor.Settings;
        Title = "探测设置";
        BackgroundColor = Ui.Background;
        _interval = Ui.Input(_initial.IntervalSeconds.ToString(), "60", Keyboard.Numeric);
        _timeout = Ui.Input(_initial.TimeoutSeconds.ToString(), "10", Keyboard.Numeric);
        _threshold = Ui.Input(_initial.SlowThresholdMs.ToString(), "1500", Keyboard.Numeric);
        _retention = Ui.Input(_initial.RetentionHours.ToString(), "168", Keyboard.Numeric);
        foreach (var site in _initial.Sites) AddSite(site);
        foreach (var proxy in _initial.Proxies) AddProxy(proxy);
        var addSite = Ui.Button("+ 添加网站");
        addSite.Clicked += (_, _) => AddSite(new("", "https://"));
        var addProxy = Ui.Button("+ 添加代理");
        addProxy.Clicked += (_, _) => AddProxy(new($"代理 {_proxies.Children.Count + 1}", "http", "127.0.0.1", 7890));
        _save.Clicked += async (_, _) => await SaveAsync();
        var parameters = new VerticalStackLayout { Spacing = 12 };
        parameters.Add(Field("探测间隔 / 秒", "10–3600，默认 60；图表始终每分钟一格", _interval));
        parameters.Add(Field("请求超时 / 秒", "1–60，超时显示红色", _timeout));
        parameters.Add(Field("黄色阈值 / 毫秒", "成功请求超过此耗时显示黄色，需小于超时", _threshold));
        parameters.Add(Field("历史保留 / 小时", "1–168，默认保留 7 天", _retention));
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 18, Padding = 22, MaximumWidthRequest = 850,
                Children =
                {
                    Ui.Text("配置仅对这台设备生效", 21, bold: true),
                    Ui.Text("保存后自动安排一轮探测；直连始终保留且不会使用系统 HTTP / SOCKS 代理。", 12, Ui.Muted),
                    Ui.Card(parameters),
                    Ui.Text("目标网站", 17, bold: true), _sites, addSite,
                    Ui.Text("代理线路", 17, bold: true),
                    Ui.Text("127.0.0.1 指当前设备。手机连接电脑上的代理时，请填写电脑的局域网 IP，并允许代理接受局域网连接。", 12, Ui.Muted),
                    _proxies, addProxy, _error, _save,
                    Ui.Text($"数据保存在此设备：\n{FileSystem.AppDataDirectory}", 11, Ui.Muted)
                }
            }
        };
    }

    private static View Field(string title, string note, Entry input) => new VerticalStackLayout
    {
        Spacing = 5,
        Children = { Ui.Text(title, 13, bold: true), Ui.Text(note, 11, Ui.Muted), input }
    };

    private void AddSite(SiteDefinition site) => _sites.Add(new SiteEditor(site, editor => _sites.Remove(editor)));
    private void AddProxy(ProxyDefinition proxy) => _proxies.Add(new ProxyEditor(proxy, editor => _proxies.Remove(editor)));

    private async Task SaveAsync()
    {
        _save.IsEnabled = false;
        try
        {
            var updated = _initial with
            {
                IntervalSeconds = Number(_interval), TimeoutSeconds = Number(_timeout), SlowThresholdMs = Number(_threshold),
                RetentionHours = Number(_retention),
                Sites = _sites.Children.OfType<SiteEditor>().Select(editor => editor.Read()).ToArray(),
                Proxies = _proxies.Children.OfType<ProxyEditor>().Select(editor => editor.Read()).ToArray()
            };
            await _monitor.SaveSettingsAsync(updated);
            await Navigation.PopAsync();
        }
        catch (SettingsValidationException exception) { _error.Text = string.Join("\n", exception.Errors); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { _error.Text = $"配置保存失败：{exception.Message}"; }
        finally { _save.IsEnabled = true; }
    }

    private static int Number(Entry entry) => int.TryParse(entry.Text, out var value) ? value : 0;
}
