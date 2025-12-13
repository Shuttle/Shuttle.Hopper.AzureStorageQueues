using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.AzureStorageQueues.Tests;

public class AzureStorageQueueOutboxFixture : OutboxFixture
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_be_able_to_use_outbox_async(bool isTransactionalEndpoint)
    {
        await TestOutboxSendingAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}", 3, isTransactionalEndpoint);
    }
}