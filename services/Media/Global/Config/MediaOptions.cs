namespace Media.Global.Config;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>
    /// Azure Storage connection string (Blob). Use Azurite for local development.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Blob host reachable by upload clients (browser/app). Authority only — do not include the
    /// account path (/devstoreaccount1); SAS URLs already contain it. When the API runs in Docker
    /// but clients run on the host, set this to e.g. http://127.0.0.1:10000 while
    /// <see cref="ConnectionString"/> uses the in-compose hostname (http://azurite:10000/...).
    /// </summary>
    public string PublicBlobEndpoint { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "tangle-media";

    public double IngressMultiplier { get; set; }

    public string WorkerCallbackSecret { get; set; } = string.Empty;

    /// <summary>Shared secret for monolith → media internal routes (<c>X-Internal-Secret</c>).</summary>
    public string InternalServiceSecret { get; set; } = string.Empty;

    public MediaContextLimitOptions Post { get; set; } = new();

    public MediaContextLimitOptions Comment { get; set; } = new();

    public MediaContextLimitOptions ChatMessage { get; set; } = new();
}

public sealed class MediaContextLimitOptions
{
    public long VideoPerFileBytes { get; set; }

    public long? VideoTotalBytes { get; set; }

    public long ImagePerFileBytes { get; set; }

    public long? ImageTotalBytes { get; set; }
}
