using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal sealed class SiteSummaryView : VerticalStackLayout
{
    public const int RowHeight = 80;
    private readonly Label _speed = Ui.Text("等待 URL 测速", 12, Ui.Accent, true);
    private readonly Label _elapsed = Ui.Text("首次采样中…", 11, Ui.Muted);
    private readonly Label _quality = Ui.Text("", 10, Ui.Muted);
    internal bool HasSpeedReading => _speed.IsLoaded && _speed.Width > 0 && _speed.Text.StartsWith("下载 ") &&
        _speed.Text.EndsWith("B/s") && _elapsed.Text.StartsWith("总耗时 ");
    internal bool HasShortSampleHint => _quality.IsLoaded && _quality.IsVisible && _quality.Text == "样本较小 · 仅供参考";
    internal string DisplayedSpeed => _speed.Text;

    public SiteSummaryView(SiteDefinition site)
    {
        HeightRequest = RowHeight;
        VerticalOptions = LayoutOptions.Center;
        Spacing = 1;
        Padding = new Thickness(0, 5, 4, 0);
        Children.Add(Ui.Text(site.Name, 14, bold: true));
        Children.Add(_speed);
        Children.Add(_elapsed);
        Children.Add(_quality);
        SemanticProperties.SetDescription(this, $"{site.Name} · {site.Url}");
    }

    public void Update(ProbeResult? sample)
    {
        _speed.Text = (sample is { Status: not ProbeStatus.Unreachable, Transfer.MegabytesPerSecond: not null } ? "下载 " : "") +
            ProbePresentation.Speed(sample);
        Ui.TextColor(_speed, sample is null ? Ui.Muted : Ui.StatusColor(sample.Status));
        _elapsed.Text = sample is null ? "等待下一轮" :
            $"{(sample.Transfer is null ? "响应头" : "总耗时")} {sample.LatencyMs:N0} ms";
        _quality.IsVisible = sample is { Transfer.UsedBrowser: true } or { Status: not ProbeStatus.Unreachable, Transfer.ShortSample: true };
        _quality.Text = sample is { Transfer.UsedBrowser: true }
            ? "旧版浏览器记录"
            : _quality.IsVisible ? "样本较小 · 仅供参考" : "";
    }
}
