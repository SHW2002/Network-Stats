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

    public ProxyEditor(ProxyDefinition proxy, Action<ProxyEditor> remove)
    {
        _name = Ui.Input(proxy.Name, "代理名称");
        _host = Ui.Input(proxy.Host, "主机 / IP");
        _port = Ui.Input(proxy.Port.ToString(), "端口", Keyboard.Numeric);
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
        Content = Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { heading, address } }, 12);
    }

    public ProxyDefinition Read() => new(_name.Text ?? "", _protocol.SelectedItem?.ToString()?.ToLowerInvariant() ?? "http",
        _host.Text ?? "", int.TryParse(_port.Text, out var port) ? port : 0);
}
