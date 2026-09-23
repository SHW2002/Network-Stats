using System.Net;
using System.Net.Sockets;
using System.Text;
using NetworkStats.App.Views;

namespace NetworkStats.App.Platforms.Windows;

internal static class SpeedTestVerifier
{
    public static async Task VerifyAsync(MainPage mainPage)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serving = ServeAsync(listener, timeout.Token);
        try
        {
            var page = await mainPage.OpenSpeedTestAsync();
            while (!page.IsLoaded || page.Width <= 0) await Task.Delay(50, timeout.Token);
            await page.RunAsync($"http://127.0.0.1:{port}/download");
            if (page.LastResult is not { Succeeded: true, Download.BytesReceived: 524288 })
                throw new InvalidOperationException($"Download speed page failed: {page.LastResult?.Error}");
            await serving;
            await mainPage.Navigation.PopAsync(false);
        }
        finally
        {
            await timeout.CancelAsync();
            try { await serving; }
            catch (OperationCanceledException) { }
        }
    }

    private static async Task ServeAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 }) { }
        await stream.WriteAsync(Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: 524288\r\nConnection: close\r\n\r\n"), cancellationToken);
        var block = new byte[64 * 1024];
        for (var i = 0; i < 8; i++)
        {
            await Task.Delay(40, cancellationToken);
            await stream.WriteAsync(block, cancellationToken);
        }
    }
}
