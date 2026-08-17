using Azure;
using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shuttle.Contract;
using Shuttle.Pipelines;
using Shuttle.Streams;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueue : ITransport, ICreateTransport, IDeleteTransport, IPurgeTransport, IDisposable
{
    private readonly Dictionary<string, AcknowledgementToken> _acknowledgementTokens = new();

    private readonly AzureStorageQueueOptions _azureStorageQueueOptions;
    private readonly TimeSpan _infiniteTimeToLive = new(0, 0, -1);
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly QueueClient _queueClient;
    private readonly Queue<ReceivedMessage> _receivedMessages = new();
    private readonly HopperOptions _hopperOptions;
    private readonly ILogger<AzureStorageQueue> _logger;

    // matches the Azure Storage Queues service default applied when no visibility timeout is specified
    private readonly TimeSpan _visibilityTimeout;
    private readonly Timer _visibilityTimeoutRenewalTimer;

    public AzureStorageQueue(HopperOptions hopperOptions, AzureStorageQueueOptions azureStorageQueueOptions, TransportUri uri, ILogger<AzureStorageQueue>? logger = null)
    {
        _logger = logger ?? NullLogger<AzureStorageQueue>.Instance;
        _hopperOptions = Guard.AgainstNull(hopperOptions);
        _azureStorageQueueOptions = Guard.AgainstNull(azureStorageQueueOptions);

        Uri = Guard.AgainstNull(uri);

        if (!string.IsNullOrWhiteSpace(_azureStorageQueueOptions.ConnectionString))
        {
            _queueClient = new(_azureStorageQueueOptions.ConnectionString, Uri.TransportName, azureStorageQueueOptions.QueueClient ?? new QueueClientOptions());
        }

        if (!string.IsNullOrWhiteSpace(_azureStorageQueueOptions.StorageAccount))
        {
            _queueClient = new(new($"https://{_azureStorageQueueOptions.StorageAccount}.queue.core.windows.net/{Uri.TransportName}"), new DefaultAzureCredential());
        }

        if (_queueClient == null)
        {
            throw new InvalidOperationException(string.Format(Resources.QueueUriException, uri.ConfigurationName));
        }

        _visibilityTimeout = _azureStorageQueueOptions.VisibilityTimeout ?? TimeSpan.FromSeconds(30);

        var renewalInterval = TimeSpan.FromTicks(_visibilityTimeout.Ticks / 2);

        if (renewalInterval < TimeSpan.FromSeconds(1))
        {
            renewalInterval = TimeSpan.FromSeconds(1);
        }

        _visibilityTimeoutRenewalTimer = new(OnVisibilityTimeoutRenewalTimer, null, renewalInterval, renewalInterval);
    }

    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        LogMessage.Operation(_logger, Uri.Uri.Scheme, Uri.TransportName, "[create/starting]");

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[create/starting]"), cancellationToken);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _queueClient.CreateIfNotExistsAsync(null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
        
        LogMessage.Operation(_logger, Uri.Uri.Scheme, Uri.TransportName, "[create/completed]");

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[create/completed]"), cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        LogMessage.Operation(_logger, Uri.Uri.Scheme, Uri.TransportName, "[delete/starting]");

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[delete/starting]"), cancellationToken);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _queueClient.DeleteIfExistsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        LogMessage.Operation(_logger, Uri.Uri.Scheme, Uri.TransportName, "[delete/completed]");

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[delete/completed]"), cancellationToken);
    }

    public void Dispose()
    {
        _visibilityTimeoutRenewalTimer.Dispose();

        _lock.Wait(CancellationToken.None);

        try
        {
            foreach (var acknowledgementToken in _acknowledgementTokens.Values)
            {
                _queueClient.SendMessage(acknowledgementToken.MessageText);
                _queueClient.DeleteMessage(acknowledgementToken.MessageId, acknowledgementToken.PopReceipt);
            }

            _acknowledgementTokens.Clear();
        }
        catch
        {
            // not much we can do here
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        LogMessage.Operation(_logger, Uri.Uri.Scheme, Uri.TransportName, "[purge/starting]");

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[purge/starting]"), cancellationToken);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _queueClient.ClearMessagesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        LogMessage.Operation(_logger, Uri.Uri.Scheme, Uri.TransportName, "[purge/completed]");

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[purge/completed]"), cancellationToken);
    }

    public async ValueTask<bool> HasPendingAsync(CancellationToken cancellationToken = default)
    {
        LogMessage.Operation(_logger, Uri.Uri.Scheme, Uri.TransportName, "[has-pending/starting]");

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[has-pending/starting]"), cancellationToken);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        bool result;

        try
        {
            result = ((QueueProperties)await _queueClient.GetPropertiesAsync(cancellationToken).ConfigureAwait(false)).ApproximateMessagesCount > 0;
        }
        finally
        {
            _lock.Release();
        }

        LogMessage.Operation(_logger, Uri.Uri.Scheme, Uri.TransportName, "[has-pending]");

        await _hopperOptions.TransportOperation.InvokeAsync(new(this, "[has-pending]", result), cancellationToken);

        return result;
    }

    public async Task<ReceivedMessage?> ReceiveAsync(IPipeline pipeline, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        ReceivedMessage? receivedMessage;

        try
        {
            if (_receivedMessages.Count == 0)
            {
                Response<QueueMessage[]>? messages = await _queueClient.ReceiveMessagesAsync(_azureStorageQueueOptions.MaxMessages, _visibilityTimeout, cancellationToken).ConfigureAwait(false);

                if (messages == null || messages.Value.Length == 0)
                {
                    return null;
                }

                foreach (var message in messages.Value)
                {
                    var acknowledgementToken = new AcknowledgementToken(message.MessageId, message.MessageText, message.PopReceipt);

                    _acknowledgementTokens.Add(acknowledgementToken.MessageId, acknowledgementToken);

                    _receivedMessages.Enqueue(new(new MemoryStream(Convert.FromBase64String(message.MessageText)), acknowledgementToken));
                }
            }

            receivedMessage = _receivedMessages.Count > 0 ? _receivedMessages.Dequeue() : null;
        }
        finally
        {
            _lock.Release();
        }

        if (receivedMessage != null)
        {
            LogMessage.MessageReceived(_logger, Uri.Uri.Scheme, Uri.TransportName);

            await _hopperOptions.MessageReceived.InvokeAsync(new(this, receivedMessage, pipeline), cancellationToken);
        }

        return receivedMessage;
    }

    public async Task ReleaseAsync(object acknowledgementToken, IPipeline pipeline, CancellationToken cancellationToken = default)
    {
        if (Guard.AgainstNull(acknowledgementToken) is not AcknowledgementToken data)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _queueClient.SendMessageAsync(data.MessageText, cancellationToken).ConfigureAwait(false);
            await _queueClient.DeleteMessageAsync(data.MessageId, data.PopReceipt, cancellationToken).ConfigureAwait(false);

            _acknowledgementTokens.Remove(data.MessageId);
        }
        finally
        {
            _lock.Release();
        }

        LogMessage.MessageReleased(_logger, Uri.Uri.Scheme, Uri.TransportName);

        await _hopperOptions.MessageReleased.InvokeAsync(new(this, acknowledgementToken, pipeline), cancellationToken);
    }

    public TransportUri Uri { get; }


    public async Task AcknowledgeAsync(object acknowledgementToken, IPipeline pipeline, CancellationToken cancellationToken = default)
    {
        if (Guard.AgainstNull(acknowledgementToken) is not AcknowledgementToken data)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _queueClient.DeleteMessageAsync(data.MessageId, data.PopReceipt, cancellationToken).ConfigureAwait(false);

            _acknowledgementTokens.Remove(data.MessageId);
        }
        finally
        {
            _lock.Release();
        }

        LogMessage.MessageAcknowledged(_logger, Uri.Uri.Scheme, Uri.TransportName);

        await _hopperOptions.MessageAcknowledged.InvokeAsync(new(this, acknowledgementToken, pipeline), cancellationToken);
    }

    public async Task SendAsync(Stream stream, IPipeline pipeline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(pipeline);

        var transportMessage = Guard.AgainstNull(pipeline.State.GetTransportMessage());

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _queueClient.SendMessageAsync(Convert.ToBase64String(await stream.ToBytesAsync().ConfigureAwait(false)), null, _infiniteTimeToLive, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        LogMessage.MessageEnqueued(_logger, Uri.Uri.Scheme, Uri.TransportName, transportMessage.MessageType, transportMessage.MessageId);

        await _hopperOptions.MessageSent.InvokeAsync(new(this, stream, pipeline), cancellationToken);
    }

    public TransportType Type => TransportType.Queue;

    private void OnVisibilityTimeoutRenewalTimer(object? state)
    {
        _ = RenewVisibilityTimeoutsAsync();
    }

    private async Task RenewVisibilityTimeoutsAsync()
    {
        if (!await _lock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            foreach (var acknowledgementToken in _acknowledgementTokens.Values)
            {
                try
                {
                    var response = await _queueClient.UpdateMessageAsync(acknowledgementToken.MessageId, acknowledgementToken.PopReceipt, visibilityTimeout: _visibilityTimeout, cancellationToken: CancellationToken.None).ConfigureAwait(false);

                    acknowledgementToken.PopReceipt = response.Value.PopReceipt;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to renew the visibility timeout for message '{MessageId}' on transport '{TransportName}' ({Scheme}).", acknowledgementToken.MessageId, Uri.TransportName, Uri.Uri.Scheme);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    internal class AcknowledgementToken(string messageId, string messageText, string popReceipt)
    {
        public string MessageId { get; } = messageId;
        public string MessageText { get; } = messageText;
        public string PopReceipt { get; set; } = popReceipt;

        public override string ToString()
        {
            return $"Message id '{MessageId}' with pop receipt '{PopReceipt}'.";
        }
    }
}