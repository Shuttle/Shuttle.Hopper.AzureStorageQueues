using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Shuttle.Hopper.AzureStorageQueues;

public static class HopperBuilderExtensions
{
    extension(HopperBuilder hopperBuilder)
    {
        public HopperBuilder UseAzureStorageQueues(Action<AzureStorageQueueBuilder> builder)
        {
            var services = hopperBuilder.Services;

            builder.Invoke(new(services));

            services.PostConfigureAll<AzureStorageQueueOptions>(options =>
            {
                options.MaxMessages = Math.Clamp(options.MaxMessages, 1, 32);
            });
            
            services.AddSingleton<IValidateOptions<AzureStorageQueueOptions>, AzureStorageQueueOptionsValidator>();
            services.AddSingleton<ITransportFactory, AzureStorageQueueFactory>();

            return hopperBuilder;
        }
    }
}