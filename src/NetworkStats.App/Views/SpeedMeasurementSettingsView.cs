using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal sealed class SpeedMeasurementSettingsView : ContentView
{
    private readonly Switch _enabled = new();
    private readonly Label _traffic = Ui.Text("", 11, Ui.Muted);

    internal bool Enabled { get => _enabled.IsToggled; set => _enabled.IsToggled = value; }
    internal string EstimatedTrafficText => _traffic.Text;

    public SpeedMeasurementSettingsView(bool enabled)
    {
        Enabled = enabled;
        Ui.Bind(_enabled, Switch.OnColorProperty, Ui.PrimaryButton);
        var row = new Grid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto)],
            ColumnSpacing = Ui.Space(10)
        };
        row.Add(Ui.Text("周期网速检测", 13, bold: true));
        row.Add(_traffic, 1);
        row.Add(_enabled, 2);
        Content = Ui.Card(new VerticalStackLayout
        {
            Spacing = Ui.Space(10),
            Children =
            {
                Ui.Text("流量控制", 17, bold: true), row,
                Ui.Text("关闭时仅获取响应头来判断可访问性和响应耗时；开启后，每轮会读取每个 URL 的响应体来计算下载速度。", 11, Ui.Muted),
                Ui.Text("按每个 URL、每条线路、每轮最多下载 1 MB 估算，不含协议开销；实际消耗通常更少。", 11, Ui.Muted)
            }
        });
    }

    internal void UpdateEstimate(int intervalSeconds, int siteCount, int routeCount)
    {
        if (intervalSeconds <= 0 || siteCount <= 0 || routeCount <= 0)
        {
            _traffic.Text = "预计流量 —";
            return;
        }
        var bytes = SpeedMeasurementTraffic.EstimateMaximumHourlyBytes(intervalSeconds, siteCount, routeCount);
        _traffic.Text = $"开启后最多 {FormatBytes(bytes)}/小时";
    }

    internal static string FormatBytes(double bytes)
    {
        var megabytes = bytes / 1_000_000;
        if (megabytes >= 1_000_000) return $"{megabytes / 1_000_000:0.##} TB";
        if (megabytes >= 1_000) return $"{megabytes / 1_000:0.##} GB";
        return $"{megabytes:0.#} MB";
    }
}
