using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal sealed class SiteSummaryView : VerticalStackLayout
{
    public const int RowHeight = 80;
    private readonly Label _speed = Ui.Text("等待 URL 测速", 12, Ui.Accent, true);
    private readonly Label _elapsed = Ui.Text("首次采样中…", 11, Ui.Muted);
    internal bool HasSpeedReading => _speed.IsLoaded && _speed.Width > 0 && _speed.Text.EndsWith("B/s");

    public SiteSummaryView(SiteDefinition site)
    {
        HeightRequest = RowHeight;
        VerticalOptions = LayoutOptions.Center;
        Spacing = 3;
        Padding = new Thickness(0, 8, 8, 0);
        Children.Add(Ui.Text(site.Name, 14, bold: true));
        Children.Add(_speed);
        Children.Add(_elapsed);
        SemanticProperties.SetDescription(this, $"{site.Name} · {site.Url}");
    }

    public void Update(ProbeResult? sample)
    {
        _speed.Text = ProbePresentation.Speed(sample);
        _speed.TextColor = sample is null ? Ui.Muted : Ui.StatusColor(sample.Status);
        _elapsed.Text = sample is null ? "等待下一轮" : $"{sample.LatencyMs:N0} ms" +
            (sample.Status == ProbeStatus.Unreachable && sample.HttpStatus is { } status ? $" · {status}" : "");
    }
}
