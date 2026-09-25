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
    private readonly Label _detail = Ui.Text("点击任意色块，查看该 URL 的可访问性与响应耗时。", 12, Ui.Muted);
    private readonly Label _policy = Ui.Text("", 11, Ui.Muted);
    private readonly Label _timelineStatus = Ui.Text("首次探测完成后显示时间图", 11, Ui.Muted);
    private readonly Button _probe = Ui.Button("立即探测", true);
    private readonly Button _pause = Ui.Button("暂停");
    private readonly MetricCard _available = new("最新可访问", "—", "全部网站 × 全部线路");
    private readonly MetricCard _latency = new("平均访问耗时", "—", "最新成功 URL 请求的总耗时");
    private readonly MetricCard _routes = new("探测线路", "1", "直接连接始终保留");
    private readonly MetricCard _rate = new("窗口可用率", "—", "仅统计有数据的色块");
    private readonly MetricCard _traffic = new("每小时预估流量", "Ping —", "测速 —", 18);
    private readonly Grid _metrics = new() { ColumnSpacing = Ui.Space(12), RowSpacing = Ui.Space(12) };
    private readonly Grid _controls = new() { ColumnSpacing = Ui.Space(16), RowSpacing = Ui.Space(10) };
    private readonly VerticalStackLayout _statusBlock;
    private readonly HorizontalStackLayout _buttons;
    private readonly VerticalStackLayout _routeList = new() { Spacing = Ui.Space(14) };
    private readonly List<RouteTimelineView> _routeViews = [];
    private readonly IDispatcherTimer _timer;
    private string? _configuration;
    private int _minutes = 60;
    internal bool HasDrawnTimelines => _routeViews.Count > 0 && _routeViews.All(route => route.HasDrawn);
    internal bool HasDrawnCurrentTheme => _routeViews.Count > 0 && _routeViews.All(route => route.HasDrawnCurrentTheme);
    internal bool HasUrlSpeedReadings => _routeViews.Any(route => route.HasSpeedReadings);
    internal bool HasShortSampleHints => _routeViews.Any(route => route.HasShortSampleHints);
    internal bool HasSeparatedTrafficEstimate => _traffic.ValueText.StartsWith("Ping ") &&
        _traffic.NoteText.StartsWith("测速 ");
    internal bool TimelineVisible => _routeList.IsVisible;
    internal DateTimeOffset? DisplayedMinute { get; private set; }
    internal string? GetSpeedText(string siteId, string routeId) =>
        _routeViews.Select(route => route.GetSpeedText(siteId, routeId)).FirstOrDefault(text => text is not null);

    public MainPage(MonitorEngine monitor, DownloadSpeedProbe downloadProbe)
    {
        _monitor = monitor;
        _downloadProbe = downloadProbe;
        Title = "网络观测站";
        Ui.Bind(this, BackgroundColorProperty, Ui.Background);
        NavigationPage.SetHasNavigationBar(this, false);
        var settings = Ui.Button("设置");
        settings.Clicked += async (_, _) => await Navigation.PushAsync(new SettingsPage(_monitor));
        var speedTest = Ui.Button("单项测速");
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
            Spacing = Ui.Space(7),
            Children = { Ui.Text($"NETWORK STATS · v{AppInfo.Current.VersionString}", 11, Ui.Accent, true), Ui.Text("网络观测站", 28, bold: true),
                Ui.Text($"{PlatformName()} · 网站可访问性与响应耗时 · 可按需开启周期网速检测", 12, Ui.Muted) }
        });
        header.Add(settings, 1);
        settings.VerticalOptions = LayoutOptions.Center;
        var range = new Picker
        {
            Title = Ui.IsDesktop ? "" : "时间范围", FontSize = 12,
            ItemsSource = new[] { "最近 1 小时", "最近 3 小时", "最近 6 小时", "最近 24 小时" }, SelectedIndex = 0,
            WidthRequest = 142
        };
        SemanticProperties.SetDescription(range, "时间范围");
        Ui.ThemePicker(range);
        range.SelectedIndexChanged += (_, _) =>
        {
            if (range.SelectedIndex < 0) return;
            _minutes = new[] { 60, 180, 360, 1440 }[range.SelectedIndex];
            Refresh();
        };
        var chartHeader = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
        if (Ui.IsDesktop)
        {
            chartHeader.RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)];
            chartHeader.RowSpacing = 3;
            chartHeader.Add(Ui.Text("可访问性时间线", 19, bold: true));
            chartHeader.Add(_timelineStatus, 0, 1);
            Grid.SetColumnSpan(_timelineStatus, 2);
        }
        else
            chartHeader.Add(new VerticalStackLayout { Spacing = 4,
                Children = { Ui.Text("可访问性时间线", 19, bold: true), _timelineStatus } });
        chartHeader.Add(range, 1);
        var legend = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        foreach (var (text, color) in new[] { ("正常", Ui.Green), ("较慢", Ui.Yellow), ("不可访问", Ui.Red), ("无数据", Ui.Empty) })
        {
            var dot = new BoxView { WidthRequest = 10, HeightRequest = 10, CornerRadius = 3, VerticalOptions = LayoutOptions.Center };
            Ui.Bind(dot, BoxView.ColorProperty, color);
            legend.Add(new HorizontalStackLayout { Spacing = 6, Margin = new Thickness(0, 0, 18, 4),
                Children = { dot, Ui.Text(text, 11, Ui.Muted) } });
        }
        _statusBlock = new VerticalStackLayout { Spacing = Ui.Space(5), Children = { _status, _schedule } };
        _buttons = new HorizontalStackLayout { Spacing = Ui.Space(10), Children = { _probe, _pause, speedTest } };
        var content = new VerticalStackLayout
        {
            Spacing = Ui.Space(18), Padding = Ui.Space(new Thickness(24, 20, 24, 30)), MaximumWidthRequest = 1400,
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
        DisplayedMinute = snapshot.WindowEnd;
        _routeList.IsVisible = snapshot.WindowEnd is not null;
        _timelineStatus.Text = snapshot.WindowEnd is { } end
            ? $"每次探测一格 · 截至 {end.ToLocalTime():MM-dd HH:mm:ss} · 探测完成后更新"
            : snapshot.Active ? "正在探测，完成后显示时间图…" : "暂无探测结果，开始探测后显示时间图";
        var routes = snapshot.Settings.GetRoutes();
        var configuration = string.Join('|', routes.Select(route => $"{route.Id}:{route.Name}:{route.SpeedMeasurementEnabled}")) +
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
        foreach (var view in _routeViews) view.Update(snapshot, relevant);
        var latest = relevant.GroupBy(sample => (sample.SiteId, sample.RouteId))
            .Select(group => group.MaxBy(sample => sample.CheckedAt)!).ToArray();
        var successful = latest.Where(sample => sample.Status != ProbeStatus.Unreachable).ToArray();
        _available.Update($"{successful.Length} / {siteIds.Count * routeIds.Count}");
        _latency.Update(successful.Length == 0 ? "—" : $"{successful.Average(sample => sample.LatencyMs):N0} ms",
            snapshot.Settings.SpeedMeasurementEnabled ? "最新成功 URL 请求的总耗时" : "最新成功请求的响应头耗时");
        _routes.Update(routes.Length.ToString(), $"直连 + {snapshot.Settings.Proxies.Length} 个代理");
        _rate.Update(relevant.Length == 0 ? "—" : $"{100.0 * relevant.Count(sample => sample.Status != ProbeStatus.Unreachable) / relevant.Length:0.0}%");
        var pingBytes = SpeedMeasurementTraffic.EstimateAvailabilityHourlyBytes(
            snapshot.Settings.IntervalSeconds, siteIds.Count, routeIds.Count);
        var speedBytes = SpeedMeasurementTraffic.EstimateEnabledHourlyBytes(snapshot.Settings);
        _traffic.Update($"Ping ≈ {SpeedMeasurementSettingsView.FormatBytes(pingBytes)}",
            snapshot.Settings.SpeedMeasurementEnabled
                ? $"测速 ≤ {SpeedMeasurementSettingsView.FormatBytes(speedBytes)}"
                : "测速 0 MB（已关闭）");
        _status.Text = !snapshot.Active ? "●  已暂停" : snapshot.Worker.Running ? "●  正在探测" : "●  持续监测中";
        Ui.TextColor(_status, snapshot.Active ? Ui.Accent : Ui.Muted);
        _schedule.Text = !snapshot.Active ? "暂停期间不会产生记录" : snapshot.Worker.Running
            ? $"正在检测 {siteIds.Count} 个网站、{routeIds.Count} 条线路…"
            : $"下次探测 {snapshot.Worker.NextRunAt?.ToLocalTime():HH:mm:ss} · 上次完成 {snapshot.Worker.LastCompletedAt?.ToLocalTime():HH:mm:ss}";
        _probe.IsEnabled = snapshot.Active && !snapshot.Worker.Running;
        _pause.Text = _monitor.UserPaused ? "继续探测" : "暂停";
        _warning.Text = snapshot.Worker.Error ?? snapshot.Warning ?? "";
        _warning.IsVisible = !string.IsNullOrEmpty(_warning.Text);
        _policy.Text = $"每次已完成探测显示一格 · 时间范围 {snapshot.RangeMinutes} 分钟 · 响应耗时：绿色 ≤ {snapshot.Settings.SlowThresholdMs:N0} ms · 黄色 > {snapshot.Settings.SlowThresholdMs:N0} ms · 超时 {snapshot.Settings.TimeoutSeconds} 秒 · 探测间隔 {snapshot.Settings.IntervalSeconds} 秒 · 历史保留 {snapshot.Settings.RetentionHours} 小时\n" +
            $"Ping 流量按每次可访问性请求约 {SpeedMeasurementTraffic.EstimatedAvailabilityBytesPerSample / 1000} KB 估算。" +
            (snapshot.Settings.SpeedMeasurementEnabled
                ? "测速流量按每个 URL 每条线路每轮最多 1 MB 估算；下载速度只使用响应体读取时间。"
                : "网速检测已关闭，不会主动读取响应体；仍可使用“单项测速”。");
    }

    private void SelectCell(CellSelection selection)
    {
        _detail.Text = ProbePresentation.Describe(selection);
        Ui.TextColor(_detail, selection.Sample is { Status: ProbeStatus.Unreachable } ? Ui.Red : Ui.Ink);
    }

    internal void RedrawTheme() { foreach (var route in _routeViews) route.RedrawTheme(); }

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
        var columns = Width >= 900 ? 5 : Width >= 650 ? 3 : 2;
        _metrics.Clear();
        _metrics.ColumnDefinitions.Clear();
        _metrics.RowDefinitions.Clear();
        for (var i = 0; i < columns; i++) _metrics.ColumnDefinitions.Add(new(GridLength.Star));
        View[] cards = [_available, _latency, _routes, _rate, _traffic];
        for (var i = 0; i < (cards.Length + columns - 1) / columns; i++)
            _metrics.RowDefinitions.Add(new(GridLength.Auto));
        for (var i = 0; i < cards.Length; i++) _metrics.Add(cards[i], i % columns, i / columns);
    }

    private static string PlatformName() => DeviceInfo.Current.Platform == DevicePlatform.WinUI ? "Windows"
        : DeviceInfo.Current.Platform == DevicePlatform.MacCatalyst ? "macOS" : DeviceInfo.Current.Platform.ToString();
}
