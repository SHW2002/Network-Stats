using NetworkStats.Models;
using NetworkStats.Monitoring;
using NetworkStats.Probing;

namespace NetworkStats.App.Views;

internal sealed class SpeedTestPage : ContentPage
{
    private const long ByteLimit = 20_000_000;
    private readonly MonitorEngine _monitor;
    private readonly DownloadSpeedProbe _probe;
    private readonly Picker _route = new() { Title = "测速线路", ItemDisplayBinding = new Binding(nameof(RouteDefinition.Name)), TextColor = Ui.Ink };
    private readonly Entry _url = Ui.Input("https://speed.cloudflare.com/__down?bytes=20000000", "https://…/file.bin", Keyboard.Url);
    private readonly Button _start = Ui.Button("开始测速", true);
    private readonly Button _cancel = Ui.Button("取消");
    private readonly Label _speed = Ui.Text("— Mbps", 32, Ui.Accent, true);
    private readonly Label _bytes = Ui.Text("等待开始", 13, Ui.Muted);
    private readonly Label _status = Ui.Text("选择线路后开始下载测速", 14, Ui.Ink);
    private readonly Label _detail = Ui.Text("", 12, Ui.Muted);
    private readonly ProgressBar _progress = new() { ProgressColor = Ui.Accent };
    private CancellationTokenSource? _running;

    internal DownloadSpeedResult? LastResult { get; private set; }

    public SpeedTestPage(MonitorEngine monitor, DownloadSpeedProbe probe)
    {
        _monitor = monitor;
        _probe = probe;
        Title = "下载测速";
        BackgroundColor = Ui.Background;
        _cancel.IsEnabled = false;
        _start.Clicked += async (_, _) => await RunAsync();
        _cancel.Clicked += (_, _) => Cancel();
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 18, Padding = 22, MaximumWidthRequest = 850,
                Children =
                {
                    Ui.Text("实际下载速度", 24, bold: true),
                    Ui.Text("单连接下载 · 手动触发 · 最长 10 秒 · 响应体上限 20 MB", 12, Ui.Muted),
                    Ui.Card(new VerticalStackLayout { Spacing = 12, Children =
                    {
                        Ui.Text("测速线路", 13, bold: true), _route,
                        Ui.Text("下载地址", 13, bold: true), _url,
                        Ui.Text("默认从 Cloudflare 下载测试数据。测指定网站时，填写该网站较大文件的直链；首页文件通常太小。", 12, Ui.Muted),
                        new HorizontalStackLayout { Spacing = 12, Children = { _start, _cancel } }
                    } }),
                    Ui.Card(new VerticalStackLayout { Spacing = 14, Children = { _status, _speed, _bytes, _progress, _detail } }),
                    Ui.Text("Mbps 为兆比特/秒，MB/s 为兆字节/秒，8 Mbps = 1 MB/s。显示从收到响应头到下载结束的平均响应体吞吐。", 12, Ui.Muted),
                    Ui.Text("结果反映此设备经所选线路到下载地址的速度，受服务器、CDN 和网络共同影响。文件只用于计数，不保存到磁盘。", 12, Ui.Muted),
                    Ui.Text("离开测速页会取消当前测试；移动端进入后台也会取消。测试期间的网络占用可能影响时间图中的响应耗时。", 12, Ui.Muted)
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var selectedId = (_route.SelectedItem as RouteDefinition)?.Id;
        var routes = _monitor.Settings.GetRoutes();
        _route.ItemsSource = routes;
        _route.SelectedItem = routes.FirstOrDefault(route => route.Id == selectedId) ?? routes[0];
    }

    protected override void OnDisappearing()
    {
        Cancel();
        base.OnDisappearing();
    }

    internal void Cancel() => _running?.Cancel();

    // 同一个入口供按钮和隔离桌面的回环测试使用。
    internal async Task RunAsync(string? downloadUrl = null)
    {
        if (_running is not null) return;
        using var cancellation = new CancellationTokenSource();
        _running = cancellation;
        LastResult = null;
        _start.IsEnabled = _route.IsEnabled = _url.IsEnabled = false;
        _cancel.IsEnabled = true;
        _status.Text = "正在连接…";
        _status.TextColor = Ui.Ink;
        _speed.Text = "— Mbps";
        _bytes.Text = "已接收 0 MB";
        _progress.Progress = 0;
        _detail.Text = "";
        try
        {
            if (downloadUrl is not null) _url.Text = downloadUrl;
            var route = (RouteDefinition)_route.SelectedItem;
            var progress = new Progress<DownloadProgress>(value =>
            {
                if (_running == cancellation && !cancellation.IsCancellationRequested)
                {
                    _status.Text = "正在下载…";
                    ShowProgress(value);
                }
            });
            var result = await _probe.MeasureAsync(new(_url.Text?.Trim() ?? "", TimeSpan.FromSeconds(10), ByteLimit),
                route, progress, cancellation.Token);
            LastResult = result;
            ShowProgress(result.Download);
            _status.Text = result.Succeeded ? result.Completion switch
            {
                DownloadCompletion.ByteLimit => "测速完成 · 达到 20 MB 上限",
                DownloadCompletion.TimeLimit => "测速完成 · 达到 10 秒时限",
                _ => "测速完成 · 文件下载完毕"
            } : "测速失败";
            _status.TextColor = result.Succeeded ? Ui.Green : Ui.Red;
            if (!result.Succeeded) _speed.Text = "— Mbps";
            _detail.Text = $"线路：{result.RouteName}\n地址：{result.Url}\n" +
                (result.Succeeded ? result.ShortSample ? "样本较短，建议使用更大的文件，或重复测试后比较。" : "本次结果为平均下载速度。" : result.Error);
        }
        catch (OperationCanceledException) { _status.Text = "已取消测速"; _speed.Text = "— Mbps"; }
        catch (Exception exception) { _status.Text = "无法测速"; _status.TextColor = Ui.Red; _detail.Text = exception.Message; }
        finally
        {
            _running = null;
            _start.IsEnabled = _route.IsEnabled = _url.IsEnabled = true;
            _cancel.IsEnabled = false;
        }
    }

    private void ShowProgress(DownloadProgress value)
    {
        _speed.Text = $"{value.MegabitsPerSecond:N2} Mbps";
        _bytes.Text = $"{value.MegabytesPerSecond:N2} MB/s · 已接收 {value.BytesReceived / 1_000_000.0:N2} MB · 总耗时 {value.TotalTime.TotalSeconds:N1} 秒";
        _progress.Progress = Math.Clamp(Math.Max(value.BytesReceived / (double)ByteLimit, value.TotalTime.TotalSeconds / 10), 0, 1);
    }
}
