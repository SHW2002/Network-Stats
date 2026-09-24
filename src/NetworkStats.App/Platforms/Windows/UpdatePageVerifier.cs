using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using NetworkStats.App.Updates;
using NetworkStats.App.Views;
using NetworkStats.Configuration;
using NetworkStats.Monitoring;
using NetworkStats.Updates;

namespace NetworkStats.App.Platforms.Windows;

internal static class UpdatePageVerifier
{
    internal static async Task VerifyAsync(MainPage mainPage, MonitorEngine monitor)
    {
        var original = monitor.Settings;
        var tag = "v9999.0.0";
        string? usedProxy = null;
        var installer = new CaptureInstaller();
        try
        {
            var settings = new SettingsPage(monitor);
            await mainPage.Navigation.PushAsync(settings, false);
            var update = await settings.OpenUpdatesAsync(installer, proxy =>
            {
                usedProxy = proxy;
                return new HttpClient(new ReleaseHandler(tag));
            });
            update.ProxyAddress = "socks5://127.0.0.1:1080";
            await update.CheckAsync();
            if (!update.CanInstall || usedProxy != update.ProxyAddress ||
                new SettingsStore(AppStorage.DataDirectory, new()).Current.UpdateProxy != usedProxy)
                throw new InvalidOperationException("Update page did not persist its proxy or offer a newer release.");
            await WindowCapture.SaveAsync((Microsoft.UI.Xaml.Window)mainPage.Window.Handler!.PlatformView!, "updates");
            await update.InstallAsync();
            if (!installer.Installed) throw new InvalidOperationException("Verified download did not reach the installer.");
            tag = "v" + AppInfo.Current.VersionString;
            await update.CheckAsync();
            if (update.CanInstall || !update.StatusText!.Contains("最新版本"))
                throw new InvalidOperationException("Update page offered to reinstall the current version.");
            await mainPage.Navigation.PopAsync(false);
            await settings.SaveAsync();
            if (monitor.Settings.UpdateProxy != usedProxy)
                throw new InvalidOperationException("Saving the parent settings page overwrote the update proxy.");
        }
        finally
        {
            while (mainPage.Navigation.NavigationStack.Last() != mainPage) await mainPage.Navigation.PopAsync(false);
            await monitor.SaveSettingsAsync(original);
        }
    }

    private static readonly byte[] Payload = "MZ isolated update UI verification"u8.ToArray();
    private static readonly string Hash = Convert.ToHexString(SHA256.HashData(Payload)).ToLowerInvariant();
    private sealed class CaptureInstaller : IUpdateInstaller
    {
        public bool IsSupported => true;
        public string Description => "隔离测试更新流程，不替换程序。";
        public bool Installed { get; private set; }
        public async Task InstallAsync(DownloadedUpdate update, CancellationToken token)
        {
            if (!(await File.ReadAllBytesAsync(update.FilePath, token)).SequenceEqual(Payload) || update.Sha256 != Hash)
                throw new InvalidDataException("Unverified payload reached installer.");
            Installed = true;
        }
    }

    private sealed class ReleaseHandler(string tag) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            HttpContent content = request.RequestUri!.AbsoluteUri == GitHubReleaseClient.LatestApiUrl
                ? new StringContent(JsonSerializer.Serialize(new
                {
                    tag_name = tag, draft = false, prerelease = false, body = "隔离更新测试",
                    assets = new[] { new { name = GitHubReleaseClient.PackageName, size = Payload.Length, digest = "sha256:" + Hash,
                        browser_download_url = $"{GitHubReleaseClient.RepositoryUrl}/releases/download/{tag}/Network-Stats.exe" } }
                })) : new ByteArrayContent(Payload);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
        }
    }
}
