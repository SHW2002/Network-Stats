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
    private DateTimeOffset _start;
    private int _minutes = 60;
    private bool _needsScroll = true;
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
            var view = new GraphicsView { Drawable = drawing, HeightRequest = SiteSummaryView.RowHeight };
            view.StartInteraction += (_, args) =>
            {
                if (args.Touches.Length == 0 || view.Width <= 0) return;
                var index = Math.Clamp((int)(args.Touches[0].X / view.Width * _minutes), 0, _minutes - 1);
                foreach (var row in _rows) { row.Drawing.SelectedIndex = -1; row.View.Invalidate(); }
                drawing.SelectedIndex = index;
                view.Invalidate();
                select(new(site, _route, _start.AddMinutes(index), drawing.Samples[index]));
            };
            SemanticProperties.SetDescription(view, $"{site.Name} 经由 {route.Name} 的每分钟可访问性时间图，点击色块查看详情");
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
            Spacing = 14,
            Children =
            {
                Ui.Text(route.Name, 16, bold: true),
                Ui.Text(route.Address ?? "DIRECT · 显式禁用系统 HTTP / SOCKS 代理", 11, Ui.Muted),
                table
            }
        };
        Content = Ui.Card(body, 18);
    }

    public void Update(MonitorSnapshot snapshot, IReadOnlyDictionary<(long, string, string), ProbeResult> samples)
    {
        _needsScroll |= _minutes != snapshot.Minutes;
        _minutes = snapshot.Minutes;
        _start = snapshot.WindowStart;
        _axis.Start = _start;
        _axis.Minutes = _minutes;
        _axisView.Invalidate();
        var first = _start.ToUnixTimeSeconds() / 60;
        foreach (var row in _rows)
        {
            var buckets = new ProbeResult?[_minutes];
            for (var index = 0; index < buckets.Length; index++)
                if (samples.TryGetValue((first + index, row.Site.Id, _route.Id), out var sample))
                    buckets[index] = sample;
            row.Drawing.Samples = buckets;
            row.Drawing.SelectedIndex = -1;
            row.View.Invalidate();
            var latest = buckets.LastOrDefault(sample => sample is not null);
            row.Summary.Update(latest);
            SemanticProperties.SetDescription(row.View,
                $"{row.Site.Name} 经由 {_route.Name}：最新记录 {Ui.StatusText(latest?.Status)}，{ProbePresentation.Speed(latest)}");
        }
        ResizePlot();
    }

    private void ResizePlot()
    {
        if (_scroll.Width <= 0) return;
        _charts.WidthRequest = Math.Max(_scroll.Width, _minutes * 5);
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
