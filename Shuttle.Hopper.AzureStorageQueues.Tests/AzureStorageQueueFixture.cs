using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.AzureStorageQueues.Tests;

[TestFixture]
public class AzureStorageQueueFixture : BasicTransportFixture
{
    [Test]
    public async Task Should_be_able_to_perform_simple_send_and_receive_async()
    {
        await TestSimpleSendAndReceiveAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}");
        await TestSimpleSendAndReceiveAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}-transient");
    }

    [Test]
    public async Task Should_be_able_to_release_a_message_async()
    {
        await TestReleaseMessageAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}");
    }

    [Test]
    public async Task Should_be_able_to_get_message_again_when_not_acknowledged_before_queue_is_disposed_async()
    {
        await TestUnacknowledgedMessageAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}");
    }
}