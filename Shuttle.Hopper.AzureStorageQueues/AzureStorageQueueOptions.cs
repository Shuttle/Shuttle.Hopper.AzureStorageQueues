using Azure.Storage.Queues;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueueOptions
{
    public const string SectionName = "Shuttle:AzureStorageQueues";
    public string ConnectionString { get; set; } = string.Empty;
    public int MaxMessages { get; set; } = 32;
    public QueueClientOptions? QueueClient { get; set; }
    public string StorageAccount { get; set; } = string.Empty;
    public TimeSpan? VisibilityTimeout { get; set; }
}