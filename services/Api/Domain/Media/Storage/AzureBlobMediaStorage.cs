using Api.Global.Config;
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
        _serviceClient = new BlobServiceClient(_options.ConnectionString);
        _containerClient = _serviceClient.GetBlobContainerClient(_options.ContainerName);
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
            var internalEndpoint = _serviceClient.Uri.GetLeftPart(UriPartial.Authority);
            uploadUrl = uploadUrl.Replace(
                internalEndpoint,
                _options.PublicBlobEndpoint.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
        }

        return new PresignedUpload(uploadUrl, objectKey, expiresAt.UtcDateTime);
    }

    public async Task<bool> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await _containerClient.GetBlobClient(objectKey).ExistsAsync(cancellationToken);
        return response.Value;
    }

    public async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _containerClient.GetBlobClient(objectKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
