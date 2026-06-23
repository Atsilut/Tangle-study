using Api.Global.Config;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace Api.Domain.Media.Storage;

public sealed class AzureBlobMediaStorage : IMediaStorage
{
    private readonly MediaOptions _options;
    private readonly BlobServiceClient _serviceClient;
    private readonly BlobContainerClient _containerClient;

    public AzureBlobMediaStorage(IOptions<MediaOptions> options)
    {
        _options = options.Value;
        _serviceClient = CreateServiceClient(_options.ConnectionString);
        _containerClient = _serviceClient.GetBlobContainerClient(_options.ContainerName);
    }

    internal static BlobServiceClient CreateServiceClient(string connectionString)
    {
        var trimmed = connectionString.Trim();
        try
        {
            return new BlobServiceClient(trimmed);
        }
        catch (FormatException)
        {
            if (TryCreateServiceClientFromParts(trimmed, out var client))
                return client;
            throw;
        }
    }

    private static bool TryCreateServiceClientFromParts(string connectionString, out BlobServiceClient client)
    {
        client = null!;
        try
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = segment.IndexOf('=');
                if (separator <= 0) continue;
                values[segment[..separator].Trim()] = segment[(separator + 1)..].Trim();
            }

            if (!values.TryGetValue("AccountName", out var accountName)
                || !values.TryGetValue("AccountKey", out var accountKey)
                || !values.TryGetValue("BlobEndpoint", out var blobEndpoint))
                return false;

            var credential = new StorageSharedKeyCredential(accountName, accountKey);
            client = new BlobServiceClient(new Uri(blobEndpoint.TrimEnd('/')), credential);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<PresignedUpload> CreatePresignedUploadAsync(
        string objectKey,
        string contentType,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        var blobClient = _containerClient.GetBlobClient(objectKey);
        if (!blobClient.CanGenerateSasUri)
            throw new InvalidOperationException("Azure Blob Storage account key is required to generate upload SAS URLs.");

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = objectKey,
            Resource = "b",
            ExpiresOn = expiresAt,
            ContentType = contentType,
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

        var uploadUrl = blobClient.GenerateSasUri(sasBuilder).ToString();
        if (!string.IsNullOrWhiteSpace(_options.PublicBlobEndpoint))
        {
            var internalAuthority = _serviceClient.Uri.GetLeftPart(UriPartial.Authority);
            var publicAuthority = new Uri(_options.PublicBlobEndpoint.TrimEnd('/'))
                .GetLeftPart(UriPartial.Authority);
            uploadUrl = uploadUrl.Replace(internalAuthority, publicAuthority, StringComparison.OrdinalIgnoreCase);
        }

        return new PresignedUpload(uploadUrl, objectKey, expiresAt.UtcDateTime);
    }

    public async Task<bool> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await _containerClient.GetBlobClient(objectKey).ExistsAsync(cancellationToken);
        return response.Value;
    }

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await _containerClient.GetBlobClient(objectKey).DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _containerClient.GetBlobClient(objectKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
