using Shuttle.Core.Contract;
using Shuttle.Extensions.Options;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueueOptions
{
    public const string SectionName = "Shuttle:AzureStorageQueues";
    public string ConnectionString { get; set; } = string.Empty;
    public int MaxMessages { get; set; } = 32;

    public string StorageAccount { get; set; } = string.Empty;
    public TimeSpan? VisibilityTimeout { get; set; }

    public AsyncEvent<ConfigureEventArgs> Configure { get; set; } = new();
}