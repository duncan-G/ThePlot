using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Grpc.Core;

namespace ThePlot.Grpc.Server.Services;

public class UploadService(
    ILogger<UploadService> logger,
    BlobServiceClient blobServiceClient) : Upload.UploadBase
{
    private const int SasExpirySeconds = 90;
    private const string UploadsContainerName = "pdf-uploads";

    public override async Task<UploadTokenResponse> RequestUploadToken(UploadTokenRequest request, ServerCallContext context)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(UploadsContainerName);

        var blobName = string.IsNullOrWhiteSpace(request.Filename)
            ? $"upload-{Guid.NewGuid():N}.pdf"
            : SanitizeBlobName(request.Filename);

        var blobClient = containerClient.GetBlobClient(blobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = UploadsContainerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddSeconds(SasExpirySeconds),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

        Uri sasUri;
        if (blobClient.CanGenerateSasUri)
        {
            sasUri = blobClient.GenerateSasUri(sasBuilder);
        }
        else
        {
            // Managed identity auth — use User Delegation SAS
            var userDelegationKey = await blobServiceClient.GetUserDelegationKeyAsync(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddSeconds(SasExpirySeconds));

            var blobUriBuilder = new BlobUriBuilder(blobClient.Uri)
            {
                Sas = sasBuilder.ToSasQueryParameters(userDelegationKey, blobServiceClient.AccountName)
            };
            sasUri = blobUriBuilder.ToUri();
        }

        logger.LogInformation("Generated upload SAS for blob {BlobName}, expires in {Seconds}s", blobName, SasExpirySeconds);

        return new UploadTokenResponse
        {
            UploadUrl = sasUri.ToString(),
            BlobName = blobName
        };
    }

    private static string SanitizeBlobName(string filename)
    {
        var name = Path.GetFileName(filename);
        if (string.IsNullOrEmpty(name)) return $"upload-{Guid.NewGuid():N}.pdf";
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) name += ".pdf";
        return $"{Guid.NewGuid():N}-{name}";
    }
}
