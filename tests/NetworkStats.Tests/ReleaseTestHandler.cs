using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NetworkStats.Updates;

namespace NetworkStats.Tests;

internal sealed class ReleaseTestHandler : HttpMessageHandler
{
    public byte[] Payload { get; } = Enumerable.Range(0, 180_123).Select(index => (byte)(index % 251)).ToArray();
    public string Tag { get; set; } = "v2.10.0";
    public bool Draft { get; set; }
    public bool Prerelease { get; set; }
    public bool WrongRepository { get; set; }
    public string UrlSuffix { get; set; } = "";
    public bool Corrupt { get; set; }
    public bool Truncate { get; set; }
    public bool WrongChecksum { get; set; }
    public bool NoChecksum { get; set; }
    public int Downloads { get; private set; }
    public string Hash => Convert.ToHexString(SHA256.HashData(Payload)).ToLowerInvariant();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var root = WrongRepository ? "https://github.com/other/repository" : GitHubReleaseClient.RepositoryUrl;
        object Asset(string name, long size, string? digest = null) => new
        {
            name, size, digest,
            browser_download_url = $"{root}/releases/download/{Tag}/{name}{UrlSuffix}"
        };
        HttpContent content;
        if (request.RequestUri!.AbsoluteUri == GitHubReleaseClient.LatestApiUrl)
        {
            var assets = new List<object> { Asset(GitHubReleaseClient.PackageName, Payload.Length, NoChecksum ? null : "sha256:" + Hash) };
            if (!NoChecksum) assets.Add(Asset("SHA256SUMS.txt", 84));
            content = new StringContent(JsonSerializer.Serialize(new { tag_name = Tag, draft = Draft, prerelease = Prerelease, body = "Release notes", assets }));
        }
        else if (request.RequestUri.AbsolutePath.EndsWith("SHA256SUMS.txt"))
            content = new StringContent($"{(WrongChecksum ? new string('0', 64) : Hash)}  Network-Stats.exe\n", Encoding.ASCII);
        else
        {
            Downloads++;
            var bytes = Payload.ToArray();
            if (Corrupt) bytes[30] ^= 1;
            if (Truncate) bytes = bytes[..^1];
            content = new StreamContent(new MemoryStream(bytes));
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
    }
}
