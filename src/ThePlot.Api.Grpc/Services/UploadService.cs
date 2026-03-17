using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Grpc.Core;

namespace ThePlot.Api.Grpc.Services;

public class UploadService(
    ILogger<UploadService> logger,
    IConfiguration configuration) : Upload.UploadBase
{
    private const int SasExpirySeconds = 90;
    private const string UploadsContainerName = "pdf-uploads";

    public override async Task<UploadTokenResponse> RequestUploadToken(UploadTokenRequest request, ServerCallContext context)
    {
        var connectionString = configuration["ConnectionStrings:pdf-storage"]
            ?? configuration["Azure:Storage:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Azure Storage connection string not configured. Set ConnectionStrings__pdf-storage (Aspire) or Azure:Storage:ConnectionString.");

        var containerClient = new BlobContainerClient(connectionString, UploadsContainerName);
        if (!containerClient.CanGenerateSasUri)
        {
            throw new RpcException(new Status(StatusCode.Internal,
                "Storage account does not support SAS token generation (ensure shared key auth)."));
        }

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

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        logger.LogInformation("Generated upload SAS for blob {BlobName}, expires in {Seconds}s", blobName, SasExpirySeconds);

        return await Task.FromResult(new UploadTokenResponse
        {
            UploadUrl = sasUri.ToString(),
            BlobName = blobName
        });
    }

    private static string SanitizeBlobName(string filename)
    {
        var name = Path.GetFileName(filename);
        if (string.IsNullOrEmpty(name)) return $"upload-{Guid.NewGuid():N}.pdf";
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) name += ".pdf";
        return $"{Guid.NewGuid():N}-{name}";
    }
}
