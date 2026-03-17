using Azure.Storage.Blobs;

namespace ThePlot.Api.Grpc.Services;

internal sealed class PdfUploadsContainerInitializer(
    ILogger<PdfUploadsContainerInitializer> logger,
    IConfiguration configuration) : IHostedService
{
    private const string UploadsContainerName = "pdf-uploads";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration["ConnectionStrings:pdf-storage"]
            ?? configuration["Azure:Storage:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            logger.LogWarning("PDF storage connection string not configured. Skipping container initialization.");
            return;
        }

        try
        {
            var containerClient = new BlobContainerClient(connectionString, UploadsContainerName);
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
