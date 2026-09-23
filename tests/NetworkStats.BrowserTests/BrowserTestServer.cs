using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NetworkStats.BrowserTests;

internal sealed class BrowserTestServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    public string Address { get; private set; } = "";
    public byte[] Compressed { get; }
    public int HttpRequests { get; private set; }
    public int BrowserRequests { get; private set; }

    public BrowserTestServer()
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
            gzip.Write(System.Text.Encoding.UTF8.GetBytes("<!doctype html><html><body>" + new string('a', 200000) + "</body></html>"));
        Compressed = output.ToArray();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _app = builder.Build();
        _app.Run(async context =>
        {
            try
            {
                if (context.Request.Path == "/timeout") { await Task.Delay(30000, context.RequestAborted); return; }
                var browser = context.Request.Headers.UserAgent.ToString().Contains("Edg/");
                if (browser) BrowserRequests++; else HttpRequests++;
                if (!browser || context.Request.Path == "/challenge")
                {
                    context.Response.StatusCode = 403;
                    context.Response.Headers["cf-mitigated"] = "challenge";
                    await context.Response.WriteAsync("<!doctype html><html><body>Browser verification required</body></html>");
                    return;
                }
                if (context.Request.Path == "/denied") { context.Response.StatusCode = 403; return; }
                if (context.Request.Path == "/redirect") { context.Response.Redirect("/page?exact=1%202"); return; }
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.Headers.ContentEncoding = "gzip";
                context.Response.ContentLength = Compressed.Length;
                await Task.Delay(100, context.RequestAborted);
                await context.Response.StartAsync();
                await context.Response.Body.WriteAsync(Compressed.AsMemory(0, Compressed.Length / 2));
                await context.Response.Body.FlushAsync();
                await Task.Delay(200, context.RequestAborted);
                await context.Response.Body.WriteAsync(Compressed.AsMemory(Compressed.Length / 2));
            }
            catch (OperationCanceledException) { }
        });
    }

    public async Task StartAsync()
    {
        await _app.StartAsync();
        Address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
