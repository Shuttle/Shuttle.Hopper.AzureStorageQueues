using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueueFactory(IOptions<HopperOptions> hopperOptions, IOptionsMonitor<AzureStorageQueueOptions> azureStorageQueueOptions)
    : ITransportFactory
{
    private readonly IOptionsMonitor<AzureStorageQueueOptions> _azureStorageQueueOptions = Guard.AgainstNull(azureStorageQueueOptions);
    private readonly HopperOptions _hopperOptions = Guard.AgainstNull(Guard.AgainstNull(hopperOptions).Value);

    public Task<ITransport> CreateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var transportUri = new TransportUri(Guard.AgainstNull(uri)).SchemeInvariant(Scheme);
        var azureStorageQueueOptions = _azureStorageQueueOptions.Get(transportUri.ConfigurationName);

        if (azureStorageQueueOptions == null)
        {
            throw new InvalidOperationException(string.Format(Hopper.Resources.TransportConfigurationNameException, transportUri.ConfigurationName));
        }

        return Task.FromResult<ITransport>(new AzureStorageQueue(_hopperOptions, azureStorageQueueOptions, transportUri));
    }

    public string Scheme => "azuresq";
}