using Azure.Storage.Blobs;

namespace ThePlot.Workers.PdfSplitting;

internal sealed class PdfChunksContainerInitializer(
    ILogger<PdfChunksContainerInitializer> logger,
    BlobServiceClient blobServiceClient) : IHostedService
{
    private const string ChunksContainerName = "pdf-chunks";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(ChunksContainerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            logger.LogInformation("Ensured blob container {ContainerName} exists", ChunksContainerName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to ensure PDF chunks container exists. Split operations may fail.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
