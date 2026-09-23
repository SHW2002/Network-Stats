using NetworkStats.Models;
using NetworkStats.Monitoring;
using NetworkStats.Probing;

namespace NetworkStats.App.Views;

public sealed class MainPage : ContentPage
{
    private readonly MonitorEngine _monitor;
    private readonly DownloadSpeedProbe _downloadProbe;
    private SpeedTestPage? _speedPage;
    private readonly Label _status = Ui.Text("正在启动", 12, Ui.Accent, true);
    private readonly Label _schedule = Ui.Text("正在准备首次探测…", 12, Ui.Muted);
    private readonly Label _warning = Ui.Text("", 12, Ui.Red);
    private readonly Label _detail = Ui.Text("点击任意色块，查看该分钟的响应耗时和错误详情。", 12, Ui.Muted);
    private readonly Label _policy = Ui.Text("", 11, Ui.Muted);
    private readonly Button _probe = Ui.Button("立即探测", true);
    private readonly Button _pause = Ui.Button("暂停");
    private readonly MetricCard _available = new("最新可访问", "—", "全部网站 × 全部线路");
    private readonly MetricCard _latency = new("平均响应", "—", "最新成功请求的响应耗时");
    private readonly MetricCard _routes = new("探测线路", "1", "直接连接始终保留");
    private readonly MetricCard _rate = new("窗口可用率", "—", "仅统计有数据的色块");
    private readonly Grid _metrics = new() { ColumnSpacing = 12, RowSpacing = 12 };
    private readonly Grid _controls = new() { ColumnSpacing = 16, RowSpacing = 10 };
    private readonly VerticalStackLayout _statusBlock;
    private readonly HorizontalStackLayout _buttons;
    private readonly VerticalStackLayout _routeList = new() { Spacing = 14 };
    private readonly List<RouteTimelineView> _routeViews = [];
    private readonly IDispatcherTimer _timer;
    private string? _configuration;
    private int _minutes = 60;
    internal bool HasDrawnTimelines => _routeViews.Count > 0 && _routeViews.All(route => route.HasDrawn);

    public MainPage(MonitorEngine monitor, DownloadSpeedProbe downloadProbe)
    {
        _monitor = monitor;
        _downloadProbe = downloadProbe;
        Title = "网络观测站";
        BackgroundColor = Ui.Background;
        NavigationPage.SetHasNavigationBar(this, false);
        var settings = Ui.Button("设置");
        settings.Clicked += async (_, _) => await Navigation.PushAsync(new SettingsPage(_monitor));
        var speedTest = Ui.Button("下载测速");
        speedTest.Clicked += async (_, _) => await OpenSpeedTestAsync();
        _probe.Clicked += (_, _) => { _monitor.RequestProbe(); Refresh(); };
        _pause.Clicked += async (_, _) =>
        {
            _pause.IsEnabled = false;
            try { await _monitor.SetPausedAsync(!_monitor.UserPaused); }
            catch (Exception exception) { await DisplayAlertAsync("操作失败", exception.Message, "知道了"); }
            finally { _pause.IsEnabled = true; Refresh(); }
        };
        var header = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        header.Add(new VerticalStackLayout
        {
            Spacing = 7,
            Children = { Ui.Text("NETWORK STATS", 11, Ui.Accent, true), Ui.Text("网络观测站", 28, bold: true),
                Ui.Text($"{PlatformName()} · 此设备的真实网络表现", 12, Ui.Muted) }
        });
        header.Add(settings, 1);
        settings.VerticalOptions = LayoutOptions.Center;
        var range = new Picker
        {
            Title = "时间范围", FontSize = 12, TextColor = Ui.Ink,
            ItemsSource = new[] { "最近 1 小时", "最近 3 小时", "最近 6 小时", "最近 24 小时" }, SelectedIndex = 0,
            WidthRequest = 142
        };
        range.SelectedIndexChanged += (_, _) =>
        {
            if (range.SelectedIndex < 0) return;
            _minutes = new[] { 60, 180, 360, 1440 }[range.SelectedIndex];
            Refresh();
        };
        var chartHeader = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
        chartHeader.Add(new VerticalStackLayout { Spacing = 4,
            Children = { Ui.Text("可访问性时间线", 19, bold: true), Ui.Text("每格 1 分钟 · 从左至右", 11, Ui.Muted) } });
        chartHeader.Add(range, 1);
        var legend = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        foreach (var (text, color) in new[] { ("正常", Ui.Green), ("较慢", Ui.Yellow), ("不可访问", Ui.Red), ("无数据", Ui.Empty) })
            legend.Add(new HorizontalStackLayout { Spacing = 6, Margin = new Thickness(0, 0, 18, 4),
                Children = { new BoxView { Color = color, WidthRequest = 10, HeightRequest = 10, CornerRadius = 3,
                    VerticalOptions = LayoutOptions.Center }, Ui.Text(text, 11, Ui.Muted) } });
        _statusBlock = new VerticalStackLayout { Spacing = 5, Children = { _status, _schedule } };
        _buttons = new HorizontalStackLayout { Spacing = 10, Children = { _probe, _pause, speedTest } };
        var content = new VerticalStackLayout
        {
            Spacing = 18, Padding = new Thickness(24, 20, 24, 30), MaximumWidthRequest = 1400,
            HorizontalOptions = LayoutOptions.Fill,
            Children = { header, _controls,
                _warning, chartHeader, legend, _routeList, _metrics, Ui.Card(_detail, 16), _policy,
                Ui.Text(DeviceInfo.Current.Platform == DevicePlatform.WinUI
                    ? "最小化后在托盘继续探测 · 双击托盘图标恢复 · 右键托盘可退出"
                    : "移动端切到后台时暂停探测，回到应用后继续；没有采样的分钟保持为空。", 11, Ui.Muted) }
        };
        Content = new ScrollView { Content = content };
        SizeChanged += (_, _) => LayoutMetrics();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(5);
        _timer.Tick += (_, _) => Refresh();
        LayoutMetrics();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _monitor.Updated += MonitorUpdated;
        _timer.Start();
        Refresh();
    }

    internal async Task<SpeedTestPage> OpenSpeedTestAsync()
    {
        _speedPage ??= new SpeedTestPage(_monitor, _downloadProbe);
        if (!Navigation.NavigationStack.Contains(_speedPage)) await Navigation.PushAsync(_speedPage, false);
        return _speedPage;
    }

    internal void CancelSpeedTest() => _speedPage?.Cancel();

    protected override void OnDisappearing()
    {
        _monitor.Updated -= MonitorUpdated;
        _timer.Stop();
        base.OnDisappearing();
    }

    private void MonitorUpdated(object? sender, EventArgs args) => Dispatcher.Dispatch(Refresh);

    private void Refresh()
    {
        var snapshot = _monitor.Snapshot(_minutes);
        var routes = snapshot.Settings.GetRoutes();
        var configuration = string.Join('|', routes.Select(route => $"{route.Id}:{route.Name}")) +
            string.Join('|', snapshot.Settings.Sites.Select(site => $"{site.Id}:{site.Name}"));
        if (configuration != _configuration)
        {
            _configuration = configuration;
            _routeViews.Clear();
            _routeList.Clear();
            foreach (var route in routes)
            {
                var view = new RouteTimelineView(route, snapshot.Settings.Sites, SelectCell);
                _routeViews.Add(view);
                _routeList.Add(view);
            }
        }
        var siteIds = snapshot.Settings.Sites.Select(site => site.Id).ToHashSet();
        var routeIds = routes.Select(route => route.Id).ToHashSet();
        var relevant = snapshot.Samples.Where(sample => siteIds.Contains(sample.SiteId) && routeIds.Contains(sample.RouteId)).ToArray();
        var samples = relevant.ToDictionary(sample => (sample.Minute, sample.SiteId, sample.RouteId));
        foreach (var view in _routeViews) view.Update(snapshot, samples);
        var latest = relevant.GroupBy(sample => (sample.SiteId, sample.RouteId))
            .Select(group => group.MaxBy(sample => sample.CheckedAt)!).ToArray();
        var successful = latest.Where(sample => sample.Status != ProbeStatus.Unreachable).ToArray();
        _available.Update($"{successful.Length} / {siteIds.Count * routeIds.Count}");
        _latency.Update(successful.Length == 0 ? "—" : $"{successful.Average(sample => sample.LatencyMs):N0} ms");
        _routes.Update(routes.Length.ToString(), $"直连 + {snapshot.Settings.Proxies.Length} 个代理");
        _rate.Update(relevant.Length == 0 ? "—" : $"{100.0 * relevant.Count(sample => sample.Status != ProbeStatus.Unreachable) / relevant.Length:0.0}%");
        _status.Text = !snapshot.Active ? "●  已暂停" : snapshot.Worker.Running ? "●  正在探测" : "●  持续监测中";
        _status.TextColor = snapshot.Active ? Ui.Accent : Ui.Muted;
        _schedule.Text = !snapshot.Active ? "暂停期间不会产生记录" : snapshot.Worker.Running
            ? $"正在检测 {siteIds.Count} 个网站、{routeIds.Count} 条线路…"
            : $"下次探测 {snapshot.Worker.NextRunAt?.ToLocalTime():HH:mm:ss} · 上次完成 {snapshot.Worker.LastCompletedAt?.ToLocalTime():HH:mm:ss}";
        _probe.IsEnabled = snapshot.Active && !snapshot.Worker.Running;
        _pause.Text = _monitor.UserPaused ? "继续探测" : "暂停";
        _warning.Text = snapshot.Worker.Error ?? snapshot.Warning ?? "";
        _warning.IsVisible = !string.IsNullOrEmpty(_warning.Text);
        _policy.Text = $"绿色 ≤ {snapshot.Settings.SlowThresholdMs:N0} ms · 黄色 > {snapshot.Settings.SlowThresholdMs:N0} ms · 超时 {snapshot.Settings.TimeoutSeconds} 秒 · 每 {snapshot.Settings.IntervalSeconds} 秒采样 · 历史保留 {snapshot.Settings.RetentionHours} 小时\n耗时为 GET 请求到响应头的时间；HTTP 4xx / 5xx 记为不可访问。";
    }

    private void SelectCell(CellSelection selection)
    {
        var prefix = $"{selection.Site.Name} / {selection.Route.Name} · {selection.Minute.ToLocalTime():MM-dd HH:mm}";
        _detail.Text = selection.Sample is not { } sample ? $"{prefix}\n无数据：这一分钟没有完成的探测。"
            : $"{prefix}\n{Ui.StatusText(sample.Status)} · {sample.LatencyMs:N0} ms" +
              (sample.HttpStatus is { } code ? $" · HTTP {code}" : "") +
              $" · 采样于 {sample.CheckedAt.ToLocalTime():HH:mm:ss}" +
              (sample.Error is { } error ? $"\n{error}" : "");
        _detail.TextColor = selection.Sample is { Status: ProbeStatus.Unreachable } ? Ui.Red : Ui.Ink;
    }

    private void LayoutMetrics()
    {
        _controls.Clear();
        _controls.ColumnDefinitions.Clear();
        _controls.RowDefinitions.Clear();
        _controls.ColumnDefinitions.Add(new(GridLength.Star));
        _controls.RowDefinitions.Add(new(GridLength.Auto));
        _controls.Add(_statusBlock);
        if (Width >= 650)
        {
            _controls.ColumnDefinitions.Add(new(GridLength.Auto));
            _controls.Add(_buttons, 1);
        }
        else
        {
            _controls.RowDefinitions.Add(new(GridLength.Auto));
            _controls.Add(_buttons, 0, 1);
        }
        var columns = Width >= 760 ? 4 : 2;
        _metrics.Clear();
        _metrics.ColumnDefinitions.Clear();
        _metrics.RowDefinitions.Clear();
        for (var i = 0; i < columns; i++) _metrics.ColumnDefinitions.Add(new(GridLength.Star));
        for (var i = 0; i < 4 / columns; i++) _metrics.RowDefinitions.Add(new(GridLength.Auto));
        View[] cards = [_available, _latency, _routes, _rate];
        for (var i = 0; i < cards.Length; i++) _metrics.Add(cards[i], i % columns, i / columns);
    }

    private static string PlatformName() => DeviceInfo.Current.Platform == DevicePlatform.WinUI ? "Windows"
        : DeviceInfo.Current.Platform == DevicePlatform.MacCatalyst ? "macOS" : DeviceInfo.Current.Platform.ToString();
}
