namespace NetworkStats.Updates;

public sealed record UpdateAsset(string Name, Uri Url, long Size, string? Sha256);
public sealed record UpdateRelease(Version Version, string Tag, string Notes, UpdateAsset? Package, UpdateAsset? Checksums)
{
    public Uri PageUrl => new($"{GitHubReleaseClient.RepositoryUrl}/releases/tag/{Uri.EscapeDataString(Tag)}");
    public bool IsNewerThan(Version current) => Version > Normalize(current);
    public static Version Normalize(Version value) => new(value.Major, value.Minor, Math.Max(0, value.Build), Math.Max(0, value.Revision));
}

public sealed record UpdateDownloadProgress(long Received, long Total)
{
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)Received / Total, 0, 1);
}

public sealed record DownloadedUpdate(string FilePath, string Sha256, Version Version);
