using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NetworkStats.Tests;

internal sealed class LocalHttpServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private int _active;
    private int _maximum;
    private int _requests;
    public string Address { get; private set; } = "";
    public string? LastRawTarget { get; private set; }
    public int MaximumConcurrency => _maximum;
    public int Requests => _requests;

    private LocalHttpServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _app = builder.Build();
        _app.Run(async context =>
        {
            Interlocked.Increment(ref _requests);
            var active = Interlocked.Increment(ref _active);
            int observed;
            do { observed = _maximum; }
            while (active > observed && Interlocked.CompareExchange(ref _maximum, active, observed) != observed);
            try
            {
                LastRawTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
                switch (context.Request.Path.Value)
                {
                    case "/slow":
                        await Task.Delay(350, context.RequestAborted);
                        context.Response.StatusCode = 204;
                        break;
                    case "/timeout":
                        await Task.Delay(10000, context.RequestAborted);
                        break;
                    case "/denied":
                        context.Response.StatusCode = 403;
                        break;
                    case "/redirect":
                        context.Response.Redirect("/fast");
                        break;
                    case "/stream":
                        await context.Response.StartAsync(context.RequestAborted);
                        await context.Response.Body.FlushAsync(context.RequestAborted);
                        await Task.Delay(10000, context.RequestAborted);
                        break;
                    default:
                        context.Response.StatusCode = 204;
                        break;
                }
            }
            catch (OperationCanceledException) { }
            finally { Interlocked.Decrement(ref _active); }
        });
    }

    public static async Task<LocalHttpServer> StartAsync()
    {
        var server = new LocalHttpServer();
        await server._app.StartAsync();
        server.Address = server._app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return server;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
