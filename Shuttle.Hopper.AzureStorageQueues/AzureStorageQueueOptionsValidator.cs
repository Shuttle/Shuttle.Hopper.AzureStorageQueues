using Microsoft.Extensions.Options;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueueOptionsValidator : IValidateOptions<AzureStorageQueueOptions>
{
    public ValidateOptionsResult Validate(string? name, AzureStorageQueueOptions options)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidateOptionsResult.Fail(Hopper.Resources.TransportConfigurationNameException);
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString) &&
            string.IsNullOrWhiteSpace(options.StorageAccount))
        {
            return ValidateOptionsResult.Fail(string.Format(Resources.QueueUriException, name, nameof(options.ConnectionString)));
        }

        return ValidateOptionsResult.Success;
    }
}