using System.IO.Compression;
using Microsoft.AspNetCore.Http;

namespace NetworkStats.Tests;

internal static class DownloadTestEndpoints
{
    public const int Size = 8 * 64 * 1024;
    public static readonly byte[] Compressed = Compress();

    public static async Task<bool> HandleAsync(HttpContext context)
    {
        switch (context.Request.Path.Value)
        {
            case "/download-small-buffered":
            case "/download-small-slow":
                context.Response.ContentLength = 4096;
                if (context.Request.Path == "/download-small-slow")
                {
                    await context.Response.StartAsync(context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
                await Task.Delay(300, context.RequestAborted);
                await context.Response.Body.WriteAsync(new byte[4096], context.RequestAborted);
                return true;
            case "/download":
            case "/download-slow":
            case "/download-late-headers":
                if (context.Request.Path == "/download-late-headers") await Task.Delay(1000, context.RequestAborted);
                context.Response.ContentLength = Size;
                await context.Response.StartAsync(context.RequestAborted);
                for (var i = 0; i < 8; i++)
                {
                    if (context.Request.Path != "/download") await Task.Delay(80, context.RequestAborted);
                    await context.Response.Body.WriteAsync(new byte[64 * 1024], context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
                return true;
            case "/download-endless":
                // 没有 Content-Length，验证客户端依然限制下载量和总时间。
                await context.Response.StartAsync(context.RequestAborted);
                while (!context.RequestAborted.IsCancellationRequested)
                {
                    await context.Response.Body.WriteAsync(new byte[16 * 1024], context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                    await Task.Delay(30, context.RequestAborted);
                }
                return true;
            case "/download-truncated":
                context.Response.ContentLength = Size;
                await context.Response.Body.WriteAsync(new byte[1024], context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
                context.Abort();
                return true;
            case "/download-compressed":
                context.Response.Headers.ContentEncoding = "gzip";
                context.Response.ContentLength = Compressed.Length;
                await context.Response.Body.WriteAsync(Compressed, context.RequestAborted);
                return true;
            case "/download-redirect":
                context.Response.Redirect("/download");
                return true;
            default:
                return false;
        }
    }

    private static byte[] Compress()
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            gzip.Write(new byte[Size]);
        return output.ToArray();
    }
}
