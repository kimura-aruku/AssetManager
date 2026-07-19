namespace AssetManager.IntegrationTests.Persistence;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "AssetManager.Tests",
            Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string GetPath(params string[] segments)
    {
        return segments.Aggregate(Path, System.IO.Path.Combine);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
