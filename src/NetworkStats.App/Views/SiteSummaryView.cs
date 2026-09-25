using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal sealed class SiteSummaryView : VerticalStackLayout
{
    private readonly Label _speed = Ui.Text("等待网络探测", 12, Ui.Accent, true);
    private readonly Label _elapsed = Ui.Text("首次采样中…", 11, Ui.Muted);
    private readonly Label _quality = Ui.Text("", 10, Ui.Muted);
    private ProbeStatus? _status;
    internal bool HasSpeedReading => _speed.IsVisible && _speed.IsLoaded && _speed.Width > 0 && _speed.Text.StartsWith("下载 ") &&
        _speed.Text.EndsWith("B/s") && _elapsed.Text.StartsWith("总耗时 ");
    internal bool HasShortSampleHint => _quality.IsLoaded && _quality.IsVisible && _quality.Text == "样本较小 · 仅供参考";
    internal string DisplayedSpeed => _speed.Text;
    internal bool SpeedVisible => _speed.IsVisible;
    internal bool UsesStatusColors
    {
        get
        {
            var expected = Ui.StatusColor(_status).Current;
            return _elapsed.TextColor.Equals(expected) && (!_speed.IsVisible || _speed.TextColor.Equals(expected));
        }
    }

    public SiteSummaryView(SiteDefinition site)
    {
        HeightRequest = Ui.SiteRowHeight;
        VerticalOptions = LayoutOptions.Center;
        Spacing = 1;
        Padding = new Thickness(0, Ui.IsDesktop ? 2 : 5, 4, 0);
        var name = Ui.Text(site.Name, 14, bold: true);
        name.LineBreakMode = LineBreakMode.TailTruncation;
        name.MaxLines = 1;
        Children.Add(name);
        Children.Add(_speed);
        Children.Add(_elapsed);
        Children.Add(_quality);
        SemanticProperties.SetDescription(this, $"{site.Name} · {site.Url}");
    }

    public void Update(ProbeResult? sample, bool speedMeasurementEnabled)
    {
        _status = sample?.Status;
        _speed.IsVisible = speedMeasurementEnabled;
        _speed.Text = !speedMeasurementEnabled ? ""
            : (sample is { Status: not ProbeStatus.Unreachable, Transfer.MegabytesPerSecond: not null } ? "下载 " : "") +
                ProbePresentation.Speed(sample);
        var color = Ui.StatusColor(sample?.Status);
        Ui.TextColor(_speed, color);
        _elapsed.Text = sample is null ? "等待下一轮" :
            $"{(sample.Transfer is null ? "响应耗时" : "总耗时")} {sample.LatencyMs:N0} ms";
        Ui.TextColor(_elapsed, color);
        _quality.IsVisible = speedMeasurementEnabled &&
            (sample is { Transfer.UsedBrowser: true } or { Status: not ProbeStatus.Unreachable, Transfer.ShortSample: true });
        _quality.Text = sample is { Transfer.UsedBrowser: true }
            ? "旧版浏览器记录"
            : sample is { Transfer.InsufficientSample: true } ? "数据过少或读取过快"
            : _quality.IsVisible ? "样本较小 · 仅供参考" : "";
    }
}
