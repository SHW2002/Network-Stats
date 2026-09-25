using NetworkStats.Models;
using NetworkStats.Monitoring;

namespace NetworkStats.App.Views;

internal sealed class RouteTimelineView : ContentView
{
    private readonly RouteDefinition _route;
    private readonly List<(SiteDefinition Site, SiteSummaryView Summary, GraphicsView View, TimelineDrawable Drawing)> _rows = [];
    private readonly VerticalStackLayout _charts = new() { Spacing = 0 };
    private readonly ScrollView _scroll;
    private readonly TimeAxisDrawable _axis = new();
    private readonly GraphicsView _axisView;
    private DateTimeOffset[] _sampleTimes = [];
    private int _rangeMinutes = 60;
    private bool _speedMeasurementEnabled;
    private bool _needsScroll = true;
    private double _viewportWidth;
    internal bool HasDrawn => _rows.Count > 0 && _rows.All(row => row.Drawing.HasDrawn);
    internal bool HasDrawnCurrentTheme => HasDrawn && _rows.All(row => row.Drawing.DrawnTheme == Application.Current!.RequestedTheme);
    internal bool HasSpeedReadings => _rows.Any(row => row.Summary.HasSpeedReading);
    internal bool HasShortSampleHints => _rows.Any(row => row.Summary.HasShortSampleHint);
    internal string? GetSpeedText(string siteId, string routeId) => routeId == _route.Id
        ? _rows.FirstOrDefault(row => row.Site.Id == siteId).Summary?.DisplayedSpeed : null;

    public RouteTimelineView(RouteDefinition route, SiteDefinition[] sites, Action<CellSelection> select)
    {
        _route = route;
        var labelColumn = new VerticalStackLayout { Spacing = 0 };
        var columnTitle = Ui.Text("目标网站", 11, Ui.Muted);
        columnTitle.HeightRequest = 24;
        labelColumn.Add(columnTitle);
        _axisView = new GraphicsView { Drawable = _axis, HeightRequest = 24 };
        _charts.Add(_axisView);
        foreach (var site in sites)
        {
            var summary = new SiteSummaryView(site);
            labelColumn.Add(summary);
            var drawing = new TimelineDrawable();
            var view = new GraphicsView { Drawable = drawing, HeightRequest = Ui.SiteRowHeight };
            view.StartInteraction += (_, args) =>
            {
                if (args.Touches.Length == 0 || view.Width <= 0) return;
                if (drawing.Samples.Length == 0) return;
                var index = Math.Clamp((int)(args.Touches[0].X / view.Width * drawing.Samples.Length), 0,
                    drawing.Samples.Length - 1);
                foreach (var row in _rows) { row.Drawing.SelectedIndex = -1; row.View.Invalidate(); }
                drawing.SelectedIndex = index;
                view.Invalidate();
                select(new(site, _route, drawing.SampleTimes[index], drawing.Samples[index], _speedMeasurementEnabled));
            };
            SemanticProperties.SetDescription(view, $"{site.Name} 经由 {route.Name} 的每次探测时间图，点击色块查看详情");
            _charts.Add(view);
            _rows.Add((site, summary, view, drawing));
        }
        _scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Default,
            Content = _charts
        };
        _scroll.SizeChanged += (_, _) => ResizePlot();
        var table = new Grid
        {
            ColumnDefinitions = [new(new GridLength(128)), new(GridLength.Star)],
            ColumnSpacing = 8
        };
        table.Add(labelColumn, 0);
        table.Add(_scroll, 1);
        var body = new VerticalStackLayout
        {
            Spacing = Ui.Space(14),
            Children =
            {
                Ui.Text(route.Name, 16, bold: true),
                Ui.Text(route.Address ?? "DIRECT · 显式禁用系统 HTTP / SOCKS 代理", 11, Ui.Muted),
                table
            }
        };
        Content = Ui.Card(body, 18);
    }

    public void Update(MonitorSnapshot snapshot, ProbeResult[] samples)
    {
        _speedMeasurementEnabled = snapshot.Settings.SpeedMeasurementEnabled && _route.SpeedMeasurementEnabled;
        _needsScroll |= _rangeMinutes != snapshot.RangeMinutes;
        _rangeMinutes = snapshot.RangeMinutes;
        var routeSamples = samples.Where(sample => sample.RouteId == _route.Id).ToArray();
        var sampleTimes = routeSamples.Select(sample => sample.SampleTime).Distinct().Order().ToArray();
        if (!_sampleTimes.SequenceEqual(sampleTimes))
        {
            var wasAtEnd = _charts.WidthRequest - _scroll.ScrollX <= _viewportWidth + 2;
            _needsScroll |= wasAtEnd;
            _sampleTimes = sampleTimes;
        }
        _axis.Now = _sampleTimes.Length == 0 ? null : snapshot.Now;
        _axis.RangeMinutes = snapshot.RangeMinutes;
        _axisView.Invalidate();
        var latestByCell = routeSamples.GroupBy(sample => (sample.SiteId, sample.SampleTime))
            .ToDictionary(group => group.Key, group => group.MaxBy(sample => sample.CheckedAt)!);
        foreach (var row in _rows)
        {
            var buckets = new ProbeResult?[_sampleTimes.Length];
            for (var index = 0; index < buckets.Length; index++)
                if (latestByCell.TryGetValue((row.Site.Id, _sampleTimes[index]), out var sample)) buckets[index] = sample;
            row.Drawing.Samples = buckets;
            row.Drawing.SampleTimes = _sampleTimes;
            row.Drawing.SelectedIndex = -1;
            row.View.Invalidate();
            var latest = buckets.LastOrDefault(sample => sample is not null);
            row.Summary.Update(latest, _speedMeasurementEnabled);
            SemanticProperties.SetDescription(row.View,
                $"{row.Site.Name} 经由 {_route.Name}：每次探测一格；最新记录 {Ui.StatusText(latest?.Status)}，" +
                (_speedMeasurementEnabled ? ProbePresentation.Speed(latest) : "网速检测已关闭"));
        }
        ResizePlot();
    }

    private void ResizePlot()
    {
        if (_scroll.Width <= 0) return;
        if (_viewportWidth != _scroll.Width)
        {
            // 正在看最新记录时，缩窄窗口后继续保留最新格；手动回看历史时保留当前位置。
            _needsScroll |= _charts.WidthRequest - _scroll.ScrollX <= _viewportWidth + 2;
            _viewportWidth = _scroll.Width;
        }
        _charts.WidthRequest = Math.Max(_scroll.Width, Math.Min(_sampleTimes.Length * 5, 16000));
        if (_needsScroll)
        {
            _needsScroll = false;
            Dispatcher.Dispatch(async () => await _scroll.ScrollToAsync(_charts.WidthRequest, 0, false));
        }
    }

    internal void RedrawTheme()
    {
        _axisView.Invalidate();
        foreach (var row in _rows) row.View.Invalidate();
    }
}
