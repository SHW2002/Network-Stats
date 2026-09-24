using System.Net;
using System.Text;
using System.Text.Json;

namespace NetworkStats.Updates;

public sealed partial class GitHubReleaseClient(HttpClient client)
{
    public const string RepositoryUrl = "https://github.com/SHW2002/Network-Stats";
    public const string LatestApiUrl = "https://api.github.com/repos/SHW2002/Network-Stats/releases/latest";
    public const string PackageName = "Network-Stats.exe";
    private const long MaximumPackageSize = 500_000_000;

    public async Task<UpdateRelease> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestApiUrl);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("NetworkStats-Updater/1.0");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        EnsureSuccess(response);
        var json = await ReadLimitedAsync(response, 1_000_000, timeout.Token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean() ||
            !Version.TryParse(tag.TrimStart('v', 'V'), out var version))
            throw new InvalidDataException("GitHub 未返回有效的正式发布版本。");
        UpdateAsset? package = null, checksums = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name is not (PackageName or "SHA256SUMS.txt")) continue;
            var url = new Uri(asset.GetProperty("browser_download_url").GetString() ?? "", UriKind.Absolute);
            ValidateAssetUrl(url, tag, name);
            var size = asset.GetProperty("size").GetInt64();
            if (size <= 0 || size > (name == PackageName ? MaximumPackageSize : 64_000))
                throw new InvalidDataException("发布附件的大小无效。");
            var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
            var hash = digest is { Length: 71 } && digest.StartsWith("sha256:", StringComparison.Ordinal)
                ? digest[7..].ToLowerInvariant() : null;
            if (hash is not null && !hash.All(Uri.IsHexDigit)) throw new InvalidDataException("发布附件的 SHA256 无效。");
            var parsed = new UpdateAsset(name, url, size, hash);
            if (name == PackageName)
            {
                if (package is not null) throw new InvalidDataException("发布包含重复的安装包。");
                package = parsed;
            }
            else
            {
                if (checksums is not null) throw new InvalidDataException("发布包含重复的校验文件。");
                checksums = parsed;
            }
        }
        return new(UpdateRelease.Normalize(version), tag,
            root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "", package, checksums);
    }

    private static void ValidateAssetUrl(Uri uri, string tag, string name)
    {
        var expected = new Uri($"{RepositoryUrl}/releases/download/{Uri.EscapeDataString(tag)}/{name}");
        if (!string.Equals(uri.AbsoluteUri, expected.AbsoluteUri, StringComparison.Ordinal))
            throw new InvalidDataException("更新附件必须来自本项目对应版本的 GitHub Release。");
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("GitHub 拒绝了请求或请求次数已达上限，请稍后重试或配置更新代理。", null, response.StatusCode);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri?.Scheme == "http")
            throw new InvalidDataException("更新请求不能降级为未加密的 HTTP 下载。");
    }

    private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, int limit, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("更新信息超过大小限制。");
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var content = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            if (content.Length + read > limit) throw new InvalidDataException("更新信息超过大小限制。");
            content.Write(buffer, 0, read);
        }
        return Encoding.UTF8.GetString(content.ToArray());
    }
}
