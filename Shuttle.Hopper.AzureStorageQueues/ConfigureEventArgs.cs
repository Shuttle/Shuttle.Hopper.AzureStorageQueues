using Azure.Storage.Queues;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.AzureStorageQueues;

public class ConfigureEventArgs(AzureStorageQueueOptions azureStorageQueueOptions, QueueClientOptions queueClientOptions, TransportUri transportUri)
{
    public AzureStorageQueueOptions AzureStorageQueueOptions { get; } = Guard.AgainstNull(azureStorageQueueOptions);
    public TransportUri TransportUri { get; } = Guard.AgainstNull(transportUri);

    public QueueClientOptions QueueClientOptions
    {
        get;
        set => field = value ?? throw new ArgumentNullException();
    } = Guard.AgainstNull(queueClientOptions);
}