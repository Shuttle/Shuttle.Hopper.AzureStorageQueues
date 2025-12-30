using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;

namespace Shuttle.Hopper.AzureStorageQueues;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAzureStorageQueues(Action<AzureStorageQueueBuilder>? builder = null)
        {
            Guard.AgainstNull(services);

            var azureStorageQueueBuilder = new AzureStorageQueueBuilder(services);

            builder?.Invoke(azureStorageQueueBuilder);

            services.AddSingleton<IValidateOptions<AzureStorageQueueOptions>, AzureStorageQueueOptionsValidator>();

            foreach (var pair in azureStorageQueueBuilder.AzureStorageQueueOptions)
            {
                services.AddOptions<AzureStorageQueueOptions>(pair.Key).Configure(options =>
                {
                    options.QueueClient = pair.Value.QueueClient;
                    options.StorageAccount = pair.Value.StorageAccount;
                    options.ConnectionString = pair.Value.ConnectionString;
                    options.VisibilityTimeout = pair.Value.VisibilityTimeout;
                    options.MaxMessages = pair.Value.MaxMessages;
                    
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

            return services;
        }
    }
}