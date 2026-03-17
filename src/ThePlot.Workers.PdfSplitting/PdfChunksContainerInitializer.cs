using Azure.Storage.Blobs;

namespace ThePlot.Workers.PdfSplitting;

internal sealed class PdfChunksContainerInitializer(
    ILogger<PdfChunksContainerInitializer> logger,
    IConfiguration configuration) : IHostedService
{
    private const string ChunksContainerName = "pdf-chunks";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration["ConnectionStrings:blobs"]
            ?? configuration["ConnectionStrings:pdf-storage"]
            ?? configuration["Azure:Storage:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            logger.LogWarning("Blob storage connection string not configured. Skipping container initialization.");
            return;
        }

        try
        {
            var containerClient = new BlobContainerClient(connectionString, ChunksContainerName);
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
