using NetworkStats.App.Updates;
using NetworkStats.Monitoring;
using NetworkStats.Updates;

namespace NetworkStats.App.Views;

internal sealed class UpdatePage : ContentPage
{
    private readonly MonitorEngine _monitor;
    private readonly IUpdateInstaller _installer;
    private readonly Func<string?, HttpClient> _clientFactory;
    private readonly Entry _proxy;
    private readonly Picker _routes = new() { Title = "使用已有代理" };
    private readonly Label _status = Ui.Text("点击检查更新，获取最新正式版本。", 13, Ui.Muted);
    private readonly Label _notes = Ui.Text("", 12, Ui.Muted);
    private readonly ProgressBar _progress = new();
    private readonly Button _check = Ui.Button("检查更新", true);
    private readonly Button _install = Ui.Button("下载并安装", true);
    private readonly Button _cancel = Ui.Button("取消");
    private CancellationTokenSource? _operation;
    private UpdateRelease? _release;
    private bool _installing;
    internal string? StatusText => _status.Text;
    internal bool CanInstall => _install.IsEnabled;
    internal string? ProxyAddress { get => _proxy.Text; set => _proxy.Text = value; }

    public UpdatePage(MonitorEngine monitor, IUpdateInstaller? installer = null, Func<string?, HttpClient>? clientFactory = null)
    {
        _monitor = monitor;
        _installer = installer ?? UpdateServices.CreateInstaller();
        _clientFactory = clientFactory ?? UpdateProxy.CreateClient;
        Title = "软件更新";
        if (UpdateServices.LastFailure() is { } failure) _status.Text = failure;
        Ui.Bind(this, BackgroundColorProperty, Ui.Background);
        _proxy = Ui.Input(monitor.Settings.UpdateProxy ?? "", "留空直连，例如 http://127.0.0.1:7890", Keyboard.Url);
        _proxy.TextChanged += (_, _) => { if (_operation is null) { _release = null; _install.IsEnabled = false; } };
        var proxies = monitor.Settings.Proxies;
        _routes.ItemsSource = new[] { "选择已有代理…", "直接连接" }.Concat(proxies.Select(proxy => proxy.Name)).ToArray();
        _routes.SelectedIndex = 0;
        _routes.SelectedIndexChanged += (_, _) =>
        {
            if (_routes.SelectedIndex == 1) _proxy.Text = "";
            else if (_routes.SelectedIndex >= 2) _proxy.Text = proxies[_routes.SelectedIndex - 2].Address;
        };
        Ui.ThemePicker(_routes);
        Ui.Bind(_progress, ProgressBar.ProgressColorProperty, Ui.Accent);
        _install.IsEnabled = _cancel.IsEnabled = false;
        _install.IsVisible = _installer.IsSupported;
        _check.Clicked += async (_, _) => await CheckAsync();
        _install.Clicked += async (_, _) => await InstallAsync();
        _cancel.Clicked += (_, _) => Cancel();
        var releases = Ui.Button("查看 GitHub 发布页");
        releases.Clicked += async (_, _) =>
        {
            try { await Launcher.Default.OpenAsync(_release?.PageUrl ?? new Uri(GitHubReleaseClient.RepositoryUrl + "/releases/latest")); }
            catch (Exception exception) { ShowError(exception); }
        };
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Spacing = Ui.Space(18), Padding = Ui.Space(22), MaximumWidthRequest = 850,
            Children =
            {
                Ui.Text($"当前版本 v{AppInfo.Current.VersionString}", 21, bold: true),
                Ui.Text(_installer.Description, 12, Ui.Muted),
                Ui.Card(new VerticalStackLayout { Spacing = Ui.Space(10), Children =
                {
                    Ui.Text("更新代理", 17, bold: true), _proxy, _routes,
                    Ui.Text("支持 HTTP、HTTPS、SOCKS5。检查和下载使用同一个代理；留空时直接连接，不使用系统代理。点击检查更新时保存。", 12, Ui.Muted)
                } }),
                new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { _check, _install, _cancel } },
                _progress, _status, _notes, releases
            }
        } };
        foreach (var button in new[] { _check, _install, _cancel }) button.Margin = new Thickness(0, 0, 8, 6);
    }

    internal async Task CheckAsync()
    {
        if (_operation is not null) return;
        _release = null;
        await RunAsync(async token =>
        {
            var proxy = UpdateProxy.Normalize(_proxy.Text);
            if (proxy != _monitor.Settings.UpdateProxy)
                await _monitor.SaveSettingsAsync(_monitor.Settings with { UpdateProxy = proxy }, token);
            _proxy.Text = proxy ?? "";
            _status.Text = "正在检查 GitHub 最新正式版本…";
            _notes.Text = "";
            using var client = _clientFactory(proxy);
            _release = await new GitHubReleaseClient(client).CheckAsync(token);
            var newer = _release.IsNewerThan(AppInfo.Current.Version);
            _status.Text = newer ? $"发现新版本 {_release.Tag}" : $"当前已是最新版本（GitHub：{_release.Tag}）。";
            _notes.Text = _release.Notes;
            if (newer && _release.Package is null) _status.Text += "\n该发布暂未提供可安装的 Windows 包。";
            if (newer && !_installer.IsSupported) _status.Text += "\n" + _installer.Description;
        });
    }

    internal async Task InstallAsync()
    {
        if (_operation is not null || _release is not { } release || !_installer.IsSupported ||
            !release.IsNewerThan(AppInfo.Current.Version) || release.Package is null) return;
        await RunAsync(async token =>
        {
            var directory = Path.Combine(UpdateServices.CacheDirectory, Guid.NewGuid().ToString("N"), "payload");
            using var client = _clientFactory(UpdateProxy.Normalize(_proxy.Text));
            var progress = new Progress<UpdateDownloadProgress>(value =>
            {
                if (_operation?.Token != token || token.IsCancellationRequested || _installing) return;
                _progress.Progress = value.Fraction;
                _status.Text = $"正在下载 {release.Tag} · {value.Received / 1_000_000.0:F1} / {value.Total / 1_000_000.0:F1} MB";
            });
            _status.Text = "正在下载安装包…";
            var downloaded = await new GitHubReleaseClient(client).DownloadAsync(release, directory, progress, token);
            token.ThrowIfCancellationRequested();
            _installing = true;
            _cancel.IsEnabled = false;
            _status.Text = "校验完成，正在安装；程序将自动重启…";
            await _installer.InstallAsync(downloaded, token);
        });
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        using var cancellation = new CancellationTokenSource();
        _operation = cancellation;
        _check.IsEnabled = _install.IsEnabled = _proxy.IsEnabled = _routes.IsEnabled = false;
        _cancel.IsEnabled = true;
        _progress.Progress = 0;
        Ui.TextColor(_status, Ui.Ink);
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { _status.Text = cancellation.IsCancellationRequested ? "已取消更新操作。" : "请求超时，请检查网络或更新代理后重试。"; }
        catch (Exception exception) { ShowError(exception); }
        finally
        {
            _operation = null;
            _installing = false;
            _check.IsEnabled = _proxy.IsEnabled = _routes.IsEnabled = true;
            _cancel.IsEnabled = false;
            _install.IsEnabled = _installer.IsSupported && _release is { Package: not null } release && release.IsNewerThan(AppInfo.Current.Version);
        }
    }

    internal void Cancel() { if (!_installing) _operation?.Cancel(); }
    private void ShowError(Exception exception) { _status.Text = exception.Message; Ui.TextColor(_status, Ui.Red); }
    protected override void OnDisappearing() { Cancel(); base.OnDisappearing(); }
    protected override bool OnBackButtonPressed() => _installing || base.OnBackButtonPressed();
}
