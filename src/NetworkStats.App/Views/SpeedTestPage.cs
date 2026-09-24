using NetworkStats.Models;
using NetworkStats.Monitoring;
using NetworkStats.Probing;

namespace NetworkStats.App.Views;

internal sealed class SpeedTestPage : ContentPage
{
    private const long ByteLimit = WebsiteProbe.SampleByteLimit;
    private readonly MonitorEngine _monitor;
    private readonly DownloadSpeedProbe _probe;
    private readonly Picker _route = new() { Title = "测速线路", ItemDisplayBinding = new Binding(nameof(RouteDefinition.Name)) };
    private readonly Picker _site = new() { Title = "目标 URL", ItemDisplayBinding = new Binding(nameof(SiteDefinition.Name)) };
    private readonly Entry _url = Ui.Input("", "从已配置的网站中选择", Keyboard.Url);
    private readonly Label _limits = Ui.Text("", 12, Ui.Muted);
    private readonly Button _start = Ui.Button("开始测速", true);
    private readonly Button _cancel = Ui.Button("取消");
    private readonly Label _speed = Ui.Text("— KB/s", 32, Ui.Accent, true);
    private readonly Label _bytes = Ui.Text("等待开始", 13, Ui.Muted);
    private readonly Label _timings = Ui.Text("访问总耗时 — ms", 13, Ui.Ink);
    private readonly Label _quality = Ui.Text("样本较小：下载量不足 1 MB 或读取不足 2 秒，速率仅供参考。", 12, Ui.Muted);
    private readonly Label _status = Ui.Text("选择线路后开始下载测速", 14, Ui.Ink);
    private readonly Label _detail = Ui.Text("", 12, Ui.Muted);
    private readonly ProgressBar _progress = new();
    private CancellationTokenSource? _running;
    private int _timeLimitSeconds;

    internal DownloadSpeedResult? LastResult { get; private set; }
    internal string DisplayedSpeed => _speed.Text;
    internal bool HasTimingBreakdown => _timings.IsLoaded && _timings.Text.Contains("访问总耗时") && _timings.Text.Contains("响应体读取");
    internal bool HasShortSampleHint => _quality.IsLoaded && _quality.IsVisible;

    public SpeedTestPage(MonitorEngine monitor, DownloadSpeedProbe probe)
    {
        _monitor = monitor;
        _probe = probe;
        Title = "URL 单项测速";
        Ui.Bind(this, BackgroundColorProperty, Ui.Background);
        Ui.ThemePicker(_route);
        Ui.ThemePicker(_site);
        Ui.Bind(_progress, ProgressBar.ProgressColorProperty, Ui.Accent);
        _url.IsReadOnly = true;
        _quality.IsVisible = false;
        _site.SelectedIndexChanged += (_, _) => _url.Text = (_site.SelectedItem as SiteDefinition)?.Url ?? "";
        _cancel.IsEnabled = false;
        _start.Clicked += async (_, _) => await RunAsync();
        _cancel.Clicked += (_, _) => Cancel();
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 18, Padding = 22, MaximumWidthRequest = 850,
                Children =
                {
                    Ui.Text("该 URL 的下载速度与访问耗时", 24, bold: true),
                    _limits,
                    Ui.Card(new VerticalStackLayout { Spacing = 12, Children =
                    {
                        Ui.Text("测速线路", 13, bold: true), _route,
                        Ui.Text("已配置的目标 URL", 13, bold: true), _site, _url,
                        Ui.Text("直接访问所选网站设置中的 URL。主界面会自动按周期测量，也可以在这里单独复测。", 12, Ui.Muted),
                        new HorizontalStackLayout { Spacing = 12, Children = { _start, _cancel } }
                    } }),
                    Ui.Card(new VerticalStackLayout { Spacing = 14, Children =
                        { _status, Ui.Text("响应体下载速度", 13, bold: true), _speed, _bytes, _timings, _quality, _progress, _detail } }),
                    Ui.Text("下载速度 = 响应体字节数 / 响应体读取时间，从收到响应头计时到读取结束。访问总耗时另含连接和响应等待。8 Mbps = 1 MB/s。", 12, Ui.Muted),
                    Ui.Text("只测量这个 URL 返回的内容，不加载网页引用的图片、脚本或视频。内容只用于计数，不保存到磁盘。", 12, Ui.Muted),
                    Ui.Text("离开测速页会取消当前测试；移动端进入后台也会取消。测试期间的网络占用可能影响时间图中的响应耗时。", 12, Ui.Muted)
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var selectedId = (_route.SelectedItem as RouteDefinition)?.Id;
        var routes = _monitor.Settings.GetRoutes();
        _route.ItemsSource = routes;
        _route.SelectedItem = routes.FirstOrDefault(route => route.Id == selectedId) ?? routes[0];
        var siteId = (_site.SelectedItem as SiteDefinition)?.Id;
        var sites = _monitor.Settings.Sites;
        _site.ItemsSource = sites;
        _site.SelectedItem = sites.FirstOrDefault(site => site.Id == siteId) ?? sites[0];
        // 列表更新但选中索引仍为 0 时，Picker 不一定触发 SelectedIndexChanged。
        _url.Text = ((SiteDefinition)_site.SelectedItem).Url;
        _timeLimitSeconds = _monitor.Settings.TimeoutSeconds;
        _limits.Text = $"直接读取该 URL · 最长 {_timeLimitSeconds} 秒 · 响应体上限 1 MB";
    }

    protected override void OnDisappearing()
    {
        Cancel();
        base.OnDisappearing();
    }

    internal void Cancel() => _running?.Cancel();

    // 同一个入口供按钮和隔离桌面的回环测试使用。
    internal async Task RunAsync()
    {
        if (_running is not null) return;
        using var cancellation = new CancellationTokenSource();
        _running = cancellation;
        LastResult = null;
        _start.IsEnabled = _route.IsEnabled = _site.IsEnabled = false;
        _cancel.IsEnabled = true;
        _status.Text = "正在连接…";
        Ui.TextColor(_status, Ui.Ink);
        _speed.Text = "— KB/s";
        _bytes.Text = "已接收 0 MB";
        _timings.Text = "访问总耗时 — ms";
        _quality.IsVisible = false;
        _progress.Progress = 0;
        _detail.Text = "";
        try
        {
            var route = (RouteDefinition)_route.SelectedItem;
            var target = (SiteDefinition)_site.SelectedItem;
            var progress = new Progress<DownloadProgress>(value =>
            {
                if (_running == cancellation && !cancellation.IsCancellationRequested)
                {
                    _status.Text = "正在下载…";
                    ShowProgress(value);
                }
            });
            var result = await _probe.MeasureAsync(new(target.Url, TimeSpan.FromSeconds(_timeLimitSeconds), ByteLimit),
                route, progress, cancellation.Token);
            LastResult = result;
            ShowProgress(result.Download);
            _status.Text = result.Succeeded ? result.Completion switch
            {
                DownloadCompletion.ByteLimit => "测速完成 · 达到 1 MB 采样上限",
                DownloadCompletion.TimeLimit => $"测速完成 · 达到 {_timeLimitSeconds} 秒时限",
                _ => "测速完成 · URL 响应体已读完"
            } : result.Completion == DownloadCompletion.EndOfFile ? "URL 响应体为空" : result.HttpStatus switch
            {
                403 => "访问被拒绝 · HTTP 403",
                401 => "需要身份认证 · HTTP 401",
                429 => "请求受限 · HTTP 429",
                _ => "测速失败"
            };
            Ui.TextColor(_status, result.Succeeded ? Ui.Green : result.Completion == DownloadCompletion.EndOfFile ? Ui.Muted : Ui.Red);
            _quality.IsVisible = result.Succeeded && result.ShortSample;
            if (!result.Succeeded) _speed.Text = "— KB/s";
            _detail.Text = $"线路：{result.RouteName}\n地址：{result.Url}\n" +
                (result.Succeeded ? "结果为该 URL 的响应体下载速度，不代表整条线路的最大带宽。" : result.Error);
        }
        catch (OperationCanceledException) { _status.Text = "已取消测速"; _speed.Text = "— KB/s"; _quality.IsVisible = false; }
        catch (Exception exception) { _status.Text = "无法测速"; Ui.TextColor(_status, Ui.Red); _detail.Text = exception.Message; _quality.IsVisible = false; }
        finally
        {
            _running = null;
            _start.IsEnabled = _route.IsEnabled = _site.IsEnabled = true;
            _cancel.IsEnabled = false;
        }
    }

    private void ShowProgress(DownloadProgress value)
    {
        _speed.Text = ProbePresentation.Rate(value.MegabytesPerSecond);
        _bytes.Text = $"{value.MegabitsPerSecond:N2} Mbps · 已接收 {value.BytesReceived / 1000.0:N1} KB";
        _timings.Text = $"访问总耗时 {value.TotalTime.TotalMilliseconds:N0} ms\n连接与响应等待 {value.ResponseWaitTime.TotalMilliseconds:N0} ms · 响应体读取 {value.TransferTime.TotalMilliseconds:N0} ms";
        _quality.IsVisible = value.ShortSample;
        _progress.Progress = Math.Clamp(Math.Max(value.BytesReceived / (double)ByteLimit, value.TotalTime.TotalSeconds / _timeLimitSeconds), 0, 1);
    }
}
