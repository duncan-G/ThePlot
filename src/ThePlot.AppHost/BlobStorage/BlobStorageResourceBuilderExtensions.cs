using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ThePlot.AppHost.BlobStorage;

static class BlobStorageResourceBuilderExtensions
{
    private const int BlobRetentionDays = 30;

    public static IResourceBuilder<AzureStorageResource> ConfigureCorsAndLifecycle(
        this IResourceBuilder<AzureStorageResource> pdfBlobStorage,
        IDistributedApplicationBuilder builder,
        EndpointReference clientEndpoint)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            pdfBlobStorage.ConfigureInfrastructure(x =>
            {
                var storageAccount = x.GetProvisionableResources().OfType<StorageAccount>().Single();
                var blobService = new BlobService("blobService") { Parent = storageAccount };
                x.Add(blobService);

                var clientHost = clientEndpoint.Property(EndpointProperty.Host);
                var clientOrigin = ReferenceExpression.Create($"https://{clientHost}");
                var clientOriginParameter = clientOrigin.AsProvisioningParameter(x, "blobCorsClientOrigin");

                blobService.CorsRules.Add(new BicepValue<StorageCorsRule>(new StorageCorsRule
                {
                    AllowedOrigins =
                    [
                        clientOriginParameter,
                    ],
                    AllowedMethods = [CorsRuleAllowedMethod.Get, CorsRuleAllowedMethod.Put, CorsRuleAllowedMethod.Options],
                    AllowedHeaders = [new BicepValue<string>("*")],
                    ExposedHeaders = [new BicepValue<string>("*")],
                    MaxAgeInSeconds = new BicepValue<int>(3600)
                }));

                var lifecyclePolicy = new StorageAccountManagementPolicy("lifecyclePolicy")
                {
                    Parent = storageAccount,
                    Rules =
                    [
                        new ManagementPolicyRule
                        {
                            Name = "delete-pdf-uploads-after-30-days",
                            IsEnabled = true,
                            RuleType = ManagementPolicyRuleType.Lifecycle,
                            Definition = new ManagementPolicyDefinition
                            {
                                Filters = new ManagementPolicyFilter
                                {
                                    BlobTypes = ["blockBlob"],
                                    PrefixMatch = ["pdf-uploads/"]
                                },
                                Actions = new ManagementPolicyAction
                                {
                                    BaseBlob = new ManagementPolicyBaseBlob
                                    {
                                        Delete = new DateAfterModification
                                        {
                                            DaysAfterModificationGreaterThan = BlobRetentionDays
                                        }
                                    }
                                }
                            }
                        },
                        new ManagementPolicyRule
                        {
                            Name = "delete-pdf-chunks-after-30-days",
                            IsEnabled = true,
                            RuleType = ManagementPolicyRuleType.Lifecycle,
                            Definition = new ManagementPolicyDefinition
                            {
                                Filters = new ManagementPolicyFilter
                                {
                                    BlobTypes = ["blockBlob"],
                                    PrefixMatch = ["pdf-chunks/"]
                                },
                                Actions = new ManagementPolicyAction
                                {
                                    BaseBlob = new ManagementPolicyBaseBlob
                                    {
                                        Delete = new DateAfterModification
                                        {
                                            DaysAfterModificationGreaterThan = BlobRetentionDays
                                        }
                                    }
                                }
                            }
                        }
                    ]
                };
                x.Add(lifecyclePolicy);
            });
        }
        else
        {
            // Configure blob storage CORS at runtime for Azurite emulator (ConfigureInfrastructure only applies to Azure provisioning)
            // See: https://github.com/dotnet/aspire/discussions/5552#discussioncomment-15239416
            pdfBlobStorage.OnResourceReady(async (_, evt, ct) =>
            {
                var logger = evt.Services.GetRequiredService<ILogger<Program>>();
                try
                {
                    var ctx = new ValueProviderContext
                    {
                        ExecutionContext = builder.ExecutionContext,
                        Network = KnownNetworkIdentifiers.LocalhostNetwork
                    };
                    var clientOrigin = await clientEndpoint.GetValueAsync(ctx, ct);
                    var clientScheme = await clientEndpoint.Property(EndpointProperty.Scheme).GetValueAsync(ctx, ct);
                    var clientHostPort = await clientEndpoint.Property(EndpointProperty.HostAndPort).GetValueAsync(ctx, ct);
                    var blobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
                    var response = await blobServiceClient.GetPropertiesAsync(cancellationToken: ct);
                    var properties = response.Value;
                    properties.Cors.Clear();
                    properties.Cors.Add(new BlobCorsRule
                    {
                        AllowedOrigins = $"{clientOrigin},{clientScheme}://*.{clientHostPort}",
                        AllowedMethods = "GET,PUT,OPTIONS",
                        AllowedHeaders = "*",
                        ExposedHeaders = "*",
                        MaxAgeInSeconds = 3600
                    });
                    await blobServiceClient.SetPropertiesAsync(properties, ct);
                    logger.LogInformation("Configured blob storage CORS for Azurite emulator.");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to configure blob storage CORS for emulator. Direct uploads may be blocked.");
                }
            });
        }

        return pdfBlobStorage;
    }
}
