using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueueFactory(IOptions<ServiceBusOptions> serviceBusOptions, IOptionsMonitor<AzureStorageQueueOptions> azureStorageQueueOptions)
    : ITransportFactory
{
    private readonly ServiceBusOptions _serviceBusOptions = Guard.AgainstNull(Guard.AgainstNull(serviceBusOptions).Value);
    private readonly IOptionsMonitor<AzureStorageQueueOptions> _azureStorageQueueOptions = Guard.AgainstNull(azureStorageQueueOptions);

    public async Task<ITransport> CreateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var transportUri = new TransportUri(Guard.AgainstNull(uri)).SchemeInvariant(Scheme);
        var azureStorageQueueOptions = _azureStorageQueueOptions.Get(transportUri.ConfigurationName);

        if (azureStorageQueueOptions == null)
        {
            throw new InvalidOperationException(string.Format(Hopper.Resources.TransportConfigurationNameException, transportUri.ConfigurationName));
        }

        var queueClientOptions = new QueueClientOptions();

        await azureStorageQueueOptions.Configure.InvokeAsync(new(azureStorageQueueOptions, queueClientOptions, transportUri), cancellationToken);

        return new AzureStorageQueue(_serviceBusOptions, azureStorageQueueOptions, queueClientOptions, transportUri);
    }

    public string Scheme => "azuresq";
}