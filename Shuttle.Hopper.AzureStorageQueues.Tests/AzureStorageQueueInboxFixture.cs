using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.AzureStorageQueues.Tests;

public class AzureStorageQueueInboxFixture : InboxFixture
{
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task Should_be_able_handle_errors_async(bool hasErrorQueue, bool isTransactionalEndpoint)
    {
        await TestInboxErrorAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}", hasErrorQueue, isTransactionalEndpoint);
    }

    [Test]
    public async Task Should_be_able_to_handle_a_deferred_message_async()
    {
        await TestInboxDeferredAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}");
    }

    [TestCase(250, false)]
    [TestCase(250, true)]
    public async Task Should_be_able_to_process_messages_concurrently_async(int msToComplete, bool isTransactionalEndpoint)
    {
        await TestInboxConcurrencyAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}", msToComplete, isTransactionalEndpoint);
    }

    [TestCase(100, true)]
    [TestCase(100, false)]
    public async Task Should_be_able_to_process_transport_timeously_async(int count, bool isTransactionalEndpoint)
    {
        await TestInboxThroughputAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}", 1000, count, 5, isTransactionalEndpoint);
    }
}