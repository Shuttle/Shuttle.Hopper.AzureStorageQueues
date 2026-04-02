using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Shuttle.Hopper.AzureStorageQueues;

public static class HopperBuilderExtensions
{
    extension(HopperBuilder hopperBuilder)
    {
        public HopperBuilder UseAzureStorageQueues(Action<AzureStorageQueueBuilder>? builder = null)
        {
            var services = hopperBuilder.Services;

            var azureStorageQueueBuilder = new AzureStorageQueueBuilder();

            builder?.Invoke(azureStorageQueueBuilder);

            services.AddSingleton<IValidateOptions<AzureStorageQueueOptions>, AzureStorageQueueOptionsValidator>();

            foreach (var pair in azureStorageQueueBuilder.AzureStorageQueueConfigureOptions)
            {
                services.AddOptions<AzureStorageQueueOptions>(pair.Key).Configure(options =>
                {
                    pair.Value(options);

                    if (options.MaxMessages < 1)
                    {
                        options.MaxMessages = 1;
                    }

                    if (options.MaxMessages > 32)
                    {
                        options.MaxMessages = 32;
                    }
                });
            }

            services.AddSingleton<ITransportFactory, AzureStorageQueueFactory>();

            return hopperBuilder;
        }
    }
}