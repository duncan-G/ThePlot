using Azure.Provisioning;
using Azure.Provisioning.EventGrid;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Storage;
using Aspire.Hosting.Azure;

namespace ThePlot.AppHost.EventGrid;

public static class EventGridResourceBuilderExtensions
{
    public static IResourceBuilder<AzureStorageResource> AddEventGridBlobSubscription(
        this IResourceBuilder<AzureStorageResource> storage,
        IResourceBuilder<AzureServiceBusResource> serviceBus,
        string containerName,
        string queueName)
    {
        storage.ConfigureInfrastructure(infra =>
        {
            var storageAccount = infra.GetProvisionableResources()
                .OfType<StorageAccount>().Single();

            // Cross-module ref: Service Bus namespace name as a provisioning parameter
            // (same pattern as blobCorsClientOrigin in AppHost.cs)
            var sbNameExpr = ReferenceExpression.Create(
                $"{serviceBus.GetOutput("name")}");
            var sbNameParam = sbNameExpr.AsProvisioningParameter(
                infra, "serviceBusNamespaceName");

            var systemTopic = new SystemTopic("pdfBlobTopic")
            {
                Source = storageAccount.Id,
                TopicType = "Microsoft.Storage.StorageAccounts",
                Location = storageAccount.Location,
            };
            infra.Add(systemTopic);

            var eventSub = new SystemTopicEventSubscription("pdfValidationSub")
            {
                Parent = systemTopic,
                Destination = new ServiceBusQueueEventSubscriptionDestination
                {
                    ResourceId = BicepFunction.Interpolate(
                        $"/subscriptions/{BicepFunction.GetSubscription().SubscriptionId}/resourceGroups/{BicepFunction.GetResourceGroup().Name}/providers/Microsoft.ServiceBus/namespaces/{sbNameParam}/queues/{queueName}")
                        .ToBicepExpression(),
                },
                Filter = new EventSubscriptionFilter
                {
                    SubjectBeginsWith = $"/blobServices/default/containers/{containerName}/",
                    IncludedEventTypes = { "Microsoft.Storage.BlobCreated" },
                },
                EventDeliverySchema = EventDeliverySchema.CloudEventSchemaV1_0,
            };
            infra.Add(eventSub);
        });

        return storage;
    }
}
