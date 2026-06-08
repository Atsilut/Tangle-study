namespace Api.Global.Config;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public bool Enabled { get; set; }

    /// <summary>
    /// Azure Storage connection string (Blob). Use Azurite for local development.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Blob endpoint reachable by upload clients (browser/app). When the API runs in Docker but
    /// clients run on the host, set this to e.g. http://127.0.0.1:10000/devstoreaccount1 while
    /// <see cref="ConnectionString"/> uses the in-compose hostname (http://azurite:10000/...).
    /// </summary>
    public string PublicBlobEndpoint { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "tangle-media";

    public double IngressMultiplier { get; set; } = 3;

    public string WorkerCallbackSecret { get; set; } = string.Empty;

    public MediaContextLimitOptions Post { get; set; } = new()
    {
        VideoPerFileBytes = 2L * 1024 * 1024 * 1024,
        VideoTotalBytes = 10L * 1024 * 1024 * 1024,
        ImagePerFileBytes = 150L * 1024 * 1024,
        ImageTotalBytes = 3L * 1024 * 1024 * 1024,
    };

    public MediaContextLimitOptions Comment { get; set; } = new()
    {
        VideoPerFileBytes = 150L * 1024 * 1024,
        ImagePerFileBytes = 75L * 1024 * 1024,
    };

    public MediaContextLimitOptions ChatMessage { get; set; } = new()
    {
        VideoPerFileBytes = 150L * 1024 * 1024,
        ImagePerFileBytes = 75L * 1024 * 1024,
    };
}

public sealed class MediaContextLimitOptions
{
    public long VideoPerFileBytes { get; set; }

    public long? VideoTotalBytes { get; set; }

    public long ImagePerFileBytes { get; set; }

    public long? ImageTotalBytes { get; set; }
}
