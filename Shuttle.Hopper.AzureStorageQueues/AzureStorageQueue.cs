using Azure;
using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Shuttle.Core.Contract;
using Shuttle.Core.Streams;

namespace Shuttle.Hopper.AzureStorageQueues;

public class AzureStorageQueue : ITransport, ICreateTransport, IDeleteTransport, IPurgeTransport, IDisposable
{
    private readonly Dictionary<string, AcknowledgementToken> _acknowledgementTokens = new();

    private readonly AzureStorageQueueOptions _azureStorageQueueOptions;
    private readonly TimeSpan _infiniteTimeToLive = new(0, 0, -1);
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly QueueClient _queueClient;
    private readonly Queue<ReceivedMessage> _receivedMessages = new();
    private readonly ServiceBusOptions _serviceBusOptions;

    public AzureStorageQueue(ServiceBusOptions serviceBusOptions, AzureStorageQueueOptions azureStorageQueueOptions, QueueClientOptions queueClientOptions, TransportUri uri)
    {
        _serviceBusOptions = Guard.AgainstNull(serviceBusOptions);
        _azureStorageQueueOptions = Guard.AgainstNull(azureStorageQueueOptions);

        Uri = Guard.AgainstNull(uri);

        if (!string.IsNullOrWhiteSpace(_azureStorageQueueOptions.ConnectionString))
        {
            _queueClient = new(_azureStorageQueueOptions.ConnectionString, Uri.TransportName, queueClientOptions);
        }

        if (!string.IsNullOrWhiteSpace(_azureStorageQueueOptions.StorageAccount))
        {
            _queueClient = new(new($"https://{_azureStorageQueueOptions.StorageAccount}.queue.core.windows.net/{Uri.TransportName}"), new DefaultAzureCredential());
        }

        if (_queueClient == null)
        {
            throw new InvalidOperationException(string.Format(Resources.QueueUriException, uri.ConfigurationName));
        }
    }

    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[create/starting]"), cancellationToken);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _queueClient.CreateIfNotExistsAsync(null, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[create/cancelled]"), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[create/completed]"), cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[drop/starting]"), cancellationToken);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _queueClient.DeleteIfExistsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[drop/cancelled]"), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[drop/completed]"), cancellationToken);
    }

    public void Dispose()
    {
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
        await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[purge/starting]"), cancellationToken);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _queueClient.ClearMessagesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[purge/cancelled]"), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[purge/completed]"), cancellationToken);
    }

    public async ValueTask<bool> HasPendingAsync(CancellationToken cancellationToken = default)
    {
        await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[has-pending/starting]"), cancellationToken);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = ((QueueProperties)await _queueClient.GetPropertiesAsync(cancellationToken).ConfigureAwait(false)).ApproximateMessagesCount > 0;

            await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[has-pending]", result), cancellationToken);

            return result;
        }
        catch (OperationCanceledException)
        {
            await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[has-pending/cancelled]", true), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        return true;
    }

    public async Task<ReceivedMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_receivedMessages.Count == 0)
            {
                Response<QueueMessage[]>? messages = null;

                try
                {
                    messages = await _queueClient.ReceiveMessagesAsync(_azureStorageQueueOptions.MaxMessages, _azureStorageQueueOptions.VisibilityTimeout, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[receive/cancelled]"), cancellationToken);
                }

                if (messages == null || messages.Value.Length == 0)
                {
                    return null;
                }

                foreach (var message in messages.Value)
                {
                    var acknowledgementToken = new AcknowledgementToken(message.MessageId, message.MessageText, message.PopReceipt);

                    if (_acknowledgementTokens.Remove(acknowledgementToken.MessageId))
                    {
                        await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[receive/refreshed]", acknowledgementToken.MessageId), cancellationToken);
                    }

                    _acknowledgementTokens.Add(acknowledgementToken.MessageId, acknowledgementToken);

                    _receivedMessages.Enqueue(new(new MemoryStream(Convert.FromBase64String(message.MessageText)), acknowledgementToken));
                }
            }

            var receivedMessage = _receivedMessages.Count > 0 ? _receivedMessages.Dequeue() : null;

            if (receivedMessage != null)
            {
                await _serviceBusOptions.MessageReceived.InvokeAsync(new(this, receivedMessage), cancellationToken);
            }

            return receivedMessage;
        }
        catch (OperationCanceledException)
        {
            await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[receive/cancelled]"), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        return null;
    }

    public async Task ReleaseAsync(object acknowledgementToken, CancellationToken cancellationToken = default)
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

            await _serviceBusOptions.MessageReleased.InvokeAsync(new(this, acknowledgementToken), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[release/cancelled]"), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        _acknowledgementTokens.Remove(data.MessageId);
    }

    public TransportUri Uri { get; }


    public async Task AcknowledgeAsync(object acknowledgementToken, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        if (Guard.AgainstNull(acknowledgementToken) is not AcknowledgementToken data)
        {
            return;
        }

        try
        {
            await _queueClient.DeleteMessageAsync(data.MessageId, data.PopReceipt, cancellationToken).ConfigureAwait(false);

            await _serviceBusOptions.MessageAcknowledged.InvokeAsync(new(this, acknowledgementToken), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[acknowledge/cancelled]"), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        _acknowledgementTokens.Remove(data.MessageId);
    }

    public async Task SendAsync(TransportMessage transportMessage, Stream stream, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(transportMessage);
        Guard.AgainstNull(stream);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _queueClient.SendMessageAsync(Convert.ToBase64String(await stream.ToBytesAsync().ConfigureAwait(false)), null, _infiniteTimeToLive, cancellationToken).ConfigureAwait(false);

            await _serviceBusOptions.MessageSent.InvokeAsync(new(this, transportMessage, stream), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await _serviceBusOptions.TransportOperation.InvokeAsync(new(this, "[enqueue/cancelled]"), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public TransportType Type => TransportType.Queue;

    internal class AcknowledgementToken(string messageId, string messageText, string popReceipt)
    {
        public string MessageId { get; } = messageId;
        public string MessageText { get; } = messageText;
        public string PopReceipt { get; } = popReceipt;
    }
}