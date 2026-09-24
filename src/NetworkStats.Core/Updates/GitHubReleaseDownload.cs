using System.Security.Cryptography;

namespace NetworkStats.Updates;

public sealed partial class GitHubReleaseClient
{
    public async Task<DownloadedUpdate> DownloadAsync(UpdateRelease release, string directory,
        IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var asset = release.Package ?? throw new InvalidOperationException("此版本没有 Windows 安装包。");
        ValidateAssetUrl(asset.Url, release.Tag, PackageName);
        if (asset.Size is <= 0 or > MaximumPackageSize) throw new InvalidDataException("安装包大小无效。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(20));
        var token = timeout.Token;
        var expectedHash = asset.Sha256;
        if (release.Checksums is { } checksum)
        {
            ValidateAssetUrl(checksum.Url, release.Tag, "SHA256SUMS.txt");
            using var manifest = await client.GetAsync(checksum.Url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            EnsureSuccess(manifest);
            var text = await ReadLimitedAsync(manifest, 64_000, token).ConfigureAwait(false);
            var hashes = text.Split('\n').Select(line => line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length == 2 && parts[1].TrimStart('*') == PackageName).Select(parts => parts[0]).ToArray();
            if (hashes.Length != 1 || hashes[0].Length != 64 || !hashes[0].All(Uri.IsHexDigit))
                throw new InvalidDataException("校验文件中缺少唯一有效的安装包 SHA256。");
            if (expectedHash is not null && !hashes[0].Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("GitHub 附件摘要与校验文件不一致。");
            expectedHash = hashes[0];
        }
        if (expectedHash is null || expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit))
            throw new InvalidDataException("此发布未提供有效的 SHA256，无法安全安装。");

        Directory.CreateDirectory(directory);
        var partial = Path.Combine(directory, PackageName + ".part");
        var destination = Path.Combine(directory, PackageName);
        if (File.Exists(partial) || File.Exists(destination)) throw new IOException("更新下载目录已被占用，请重新下载。");
        try
        {
            using var response = await client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            EnsureSuccess(response);
            if (response.Content.Headers.ContentLength is { } length && length != asset.Size)
                throw new InvalidDataException("安装包大小与发布信息不一致。");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true))
            {
                await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                var buffer = new byte[64 * 1024];
                long received = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    received += count;
                    if (received > asset.Size) throw new InvalidDataException("下载内容超过发布附件的大小。");
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    progress?.Report(new(received, asset.Size));
                }
                if (received != asset.Size) throw new InvalidDataException("下载未完成，请重试。");
                await output.FlushAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("安装包 SHA256 校验失败，已取消安装，请重新下载。");
            File.Move(partial, destination);
            return new(destination, actualHash, release.Version);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
}
