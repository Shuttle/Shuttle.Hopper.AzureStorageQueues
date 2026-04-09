using Shuttle.Contract;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueueBuilder
{
    internal readonly Dictionary<string, Action<AzureStorageQueueOptions>> AzureStorageQueueConfigureOptions = new();

    public AzureStorageQueueBuilder Configure(string name, Action<AzureStorageQueueOptions> configureOptions)
    {
        Guard.AgainstEmpty(name);
        Guard.AgainstNull(configureOptions);

        AzureStorageQueueConfigureOptions.Remove(name);
        AzureStorageQueueConfigureOptions.Add(name, configureOptions);

        return this;
    }
}