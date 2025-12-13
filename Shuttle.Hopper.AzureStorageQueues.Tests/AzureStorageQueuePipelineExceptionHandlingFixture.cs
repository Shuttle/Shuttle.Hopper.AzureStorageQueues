using NUnit.Framework;
using Shuttle.Hopper.Testing;

namespace Shuttle.Hopper.AzureStorageQueues.Tests;

public class AzureStorageQueuePipelineExceptionHandlingFixture : PipelineExceptionFixture
{
    [Test]
    public async Task Should_be_able_to_handle_exceptions_in_receive_stage_of_receive_pipeline_async()
    {
        await TestExceptionHandlingAsync(AzureStorageQueueConfiguration.GetServiceCollection(), "azuresq://azure/{0}");
    }
}