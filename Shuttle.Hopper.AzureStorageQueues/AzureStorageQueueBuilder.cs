using Shuttle.Core.Contract;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueueBuilder()
{
    internal readonly Dictionary<string, Action<AzureStorageQueueOptions>> AzureStorageQueueOptions = new();

    public AzureStorageQueueBuilder Configure(string name, Action<AzureStorageQueueOptions> configure)
    {
        Guard.AgainstEmpty(name);
        Guard.AgainstNull(configure);

        AzureStorageQueueOptions.Remove(name);
        AzureStorageQueueOptions.Add(name, configure);

        return this;
    }
}