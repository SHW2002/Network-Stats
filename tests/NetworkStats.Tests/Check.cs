namespace NetworkStats.Tests;

internal static class Check
{
    public static void That(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static async Task EventuallyAsync(Func<bool> predicate, string message, int milliseconds = 5000)
    {
        using var timeout = new CancellationTokenSource(milliseconds);
        while (!predicate())
        {
            if (timeout.IsCancellationRequested) throw new InvalidOperationException(message);
            await Task.Delay(20);
        }
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "networkstats-tests-" + Guid.NewGuid().ToString("N"));
    public TemporaryDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
