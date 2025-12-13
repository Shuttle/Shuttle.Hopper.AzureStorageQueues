using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.AzureStorageQueues.Tests;

public class AzureStorageQueueDeferredMessageFixture : DeferredFixture
{
    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public async Task Should_be_able_to_perform_full_processing_async(bool isTransactionalEndpoint)
    {
        await TestDeferredProcessingAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}", isTransactionalEndpoint);
    }
}