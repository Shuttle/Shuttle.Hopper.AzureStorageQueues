using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueueBuilder(IServiceCollection services)
{
    internal readonly Dictionary<string, AzureStorageQueueOptions> AzureStorageQueueOptions = new();

    public IServiceCollection Services { get; } = Guard.AgainstNull(services);

    public AzureStorageQueueBuilder AddOptions(string name, AzureStorageQueueOptions azureStorageQueueOptions)
    {
        Guard.AgainstEmpty(name);
        Guard.AgainstNull(azureStorageQueueOptions);

        AzureStorageQueueOptions.Remove(name);
        AzureStorageQueueOptions.Add(name, azureStorageQueueOptions);

        return this;
    }
}