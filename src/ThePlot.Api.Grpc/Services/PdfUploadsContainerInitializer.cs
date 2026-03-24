using Azure.Storage.Blobs;

namespace ThePlot.Api.Grpc.Services;

internal sealed class PdfUploadsContainerInitializer(
    ILogger<PdfUploadsContainerInitializer> logger,
    BlobServiceClient blobServiceClient) : IHostedService
{
    private const string UploadsContainerName = "pdf-uploads";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(UploadsContainerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            logger.LogInformation("Ensured blob container {ContainerName} exists", UploadsContainerName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to ensure PDF uploads container exists. Upload token requests may fail.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
