using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shuttle.Hopper.AzureStorageQueues.Tests;

public static class AzureStorageQueueConfiguration
{
    public static IServiceCollection GetServiceCollection()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddHopper()
            .UseAzureStorageQueues(builder =>
            {
                builder.Configure("azure", options =>
                {
                    options.ConnectionString = "UseDevelopmentStorage=true";
                    options.MaxMessages = 20;
                    options.VisibilityTimeout = null;
                });
            });

        return services;
    }
}