using Microsoft.Extensions.DependencyInjection;
using Shuttle.Contract;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueueBuilder(IServiceCollection services)
{
    public AzureStorageQueueBuilder Configure(string name, Action<AzureStorageQueueOptions> configureOptions)
    {
        Guard.AgainstNull(services)
            .AddOptions<AzureStorageQueueOptions>(Guard.AgainstEmpty(name))
            .Configure(Guard.AgainstNull(configureOptions));
        
        return this;
    }
}