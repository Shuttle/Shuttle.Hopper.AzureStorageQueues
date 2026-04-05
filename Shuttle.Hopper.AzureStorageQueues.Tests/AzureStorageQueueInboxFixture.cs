using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.AzureStorageQueues.Tests;

public class AzureStorageQueueInboxFixture : InboxFixture
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_be_able_handle_errors_async(bool hasErrorQueue)
    {
        await TestInboxErrorAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}", hasErrorQueue);
    }

    [Test]
    public async Task Should_be_able_to_handle_a_deferred_message_async()
    {
        await TestInboxDeferredAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}");
    }

    [Test]
    public async Task Should_be_able_to_process_messages_concurrently_async()
    {
        await TestInboxConcurrencyAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}", TimeSpan.FromMilliseconds(250));
    }

    [Test]
    public async Task Should_be_able_to_process_transport_timeously_async()
    {
        await TestInboxThroughputAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}", 1000, 5, TimeSpan.FromSeconds(500));
    }
}