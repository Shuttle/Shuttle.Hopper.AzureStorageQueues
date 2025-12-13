using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueueFactory(IOptions<ServiceBusOptions> serviceBusOptions, IOptionsMonitor<AzureStorageQueueOptions> azureStorageQueueOptions)
    : ITransportFactory
{
    private readonly IOptionsMonitor<AzureStorageQueueOptions> _azureStorageQueueOptions = Guard.AgainstNull(azureStorageQueueOptions);
    private readonly ServiceBusOptions _serviceBusOptions = Guard.AgainstNull(Guard.AgainstNull(serviceBusOptions).Value);

    public async Task<ITransport> CreateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var transportUri = new TransportUri(Guard.AgainstNull(uri)).SchemeInvariant(Scheme);
        var azureStorageQueueOptions = _azureStorageQueueOptions.Get(transportUri.ConfigurationName);

        if (azureStorageQueueOptions == null)
        {
            throw new InvalidOperationException(string.Format(Hopper.Resources.TransportConfigurationNameException, transportUri.ConfigurationName));
        }

        return new AzureStorageQueue(_serviceBusOptions, azureStorageQueueOptions, transportUri);
    }

    public string Scheme => "azuresq";
}