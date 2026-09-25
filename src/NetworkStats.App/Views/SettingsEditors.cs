using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal sealed class SiteEditor : ContentView
{
    private readonly Entry _name;
    private readonly Entry _url;

    public SiteEditor(SiteDefinition site, Action<SiteEditor> remove)
    {
        _name = Ui.Input(site.Name, "网站名称");
        _url = Ui.Input(site.Url, "https://example.com/", Keyboard.Url);
        var delete = Ui.Button("移除");
        delete.Clicked += (_, _) => remove(this);
        var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 10 };
        row.Add(_name);
        row.Add(delete, 1);
        Content = Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { row, _url } }, 12);
    }

    public SiteDefinition Read() => new(_name.Text ?? "", _url.Text ?? "");
}

internal sealed class ProxyEditor : ContentView
{
    private readonly Entry _name;
    private readonly Entry _host;
    private readonly Entry _port;
    private readonly Picker _protocol;
    private readonly Switch _speedMeasurement;

    internal bool SpeedMeasurementEnabled { get => _speedMeasurement.IsToggled; set => _speedMeasurement.IsToggled = value; }
    internal event EventHandler? Changed;

    public ProxyEditor(ProxyDefinition proxy, Action<ProxyEditor> remove)
    {
        _name = Ui.Input(proxy.Name, "代理名称");
        _host = Ui.Input(proxy.Host, "主机 / IP");
        _port = Ui.Input(proxy.Port.ToString(), "端口", Keyboard.Numeric);
        _speedMeasurement = new Switch { IsToggled = proxy.SpeedMeasurementEnabled };
        _speedMeasurement.Toggled += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        Ui.Bind(_speedMeasurement, Switch.OnColorProperty, Ui.PrimaryButton);
        _protocol = new Picker { Title = "协议", ItemsSource = new[] { "HTTP", "HTTPS", "SOCKS5" },
            FontSize = 13 };
        Ui.ThemePicker(_protocol);
        _protocol.SelectedIndex = proxy.Protocol switch { "https" => 1, "socks5" => 2, _ => 0 };
        var delete = Ui.Button("移除");
        delete.Clicked += (_, _) => remove(this);
        var heading = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 10 };
        heading.Add(_name);
        heading.Add(delete, 1);
        var address = new Grid { ColumnDefinitions = [new(new GridLength(88)), new(GridLength.Star), new(new GridLength(84))], ColumnSpacing = 8 };
        address.Add(_protocol);
        address.Add(_host, 1);
        address.Add(_port, 2);
        var speed = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        speed.Add(Ui.Text("此线路启用周期网速检测", 12, bold: true));
        speed.Add(_speedMeasurement, 1);
        Content = Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { heading, address, speed } }, 12);
    }

    public ProxyDefinition Read() => new(_name.Text ?? "", _protocol.SelectedItem?.ToString()?.ToLowerInvariant() ?? "http",
        _host.Text ?? "", int.TryParse(_port.Text, out var port) ? port : 0, SpeedMeasurementEnabled);
}
