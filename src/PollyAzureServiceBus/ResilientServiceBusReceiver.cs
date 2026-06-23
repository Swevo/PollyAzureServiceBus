namespace PollyAzureServiceBus;

/// <summary>
/// A resilient Azure Service Bus receiver that wraps <see cref="ServiceBusReceiver"/>
/// with a Polly v8 pipeline: retry → circuit breaker → timeout.
/// </summary>
public sealed class ResilientServiceBusReceiver : IAsyncDisposable
{
    private readonly ServiceBusReceiver _inner;
    private readonly ResiliencePipeline _pipeline;
    private readonly PollyServiceBusOptions _options;

    /// <summary>The entity path this receiver reads from.</summary>
    public string EntityPath => _inner.EntityPath;

    /// <summary>Initialises the resilient receiver.</summary>
    public ResilientServiceBusReceiver(ServiceBusReceiver inner, PollyServiceBusOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);
        _inner = inner;
        _options = options;
        _pipeline = PipelineBuilder.Build(options);
    }

    /// <summary>
    /// Receives a single message with Polly resilience applied.
    /// Returns <c>null</c> if no message arrives within <paramref name="maxWaitTime"/>.
    /// </summary>
    public async Task<ServiceBusReceivedMessage?> ReceiveMessageAsync(
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default)
    {
        return await _pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                return await _inner.ReceiveMessageAsync(maxWaitTime, ct)
                    .WaitAsync(ct).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (_options.TransientFailureReasons.Contains(ex.Reason))
            {
                throw new TransientServiceBusException(ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Receives a batch of messages with Polly resilience applied.
    /// </summary>
    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
        int maxMessages,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default)
    {
        return await _pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                return await _inner.ReceiveMessagesAsync(maxMessages, maxWaitTime, ct)
                    .WaitAsync(ct).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (_options.TransientFailureReasons.Contains(ex.Reason))
            {
                throw new TransientServiceBusException(ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Completes a received message, removing it from the entity.</summary>
    public async Task CompleteMessageAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                await _inner.CompleteMessageAsync(message, ct).WaitAsync(ct).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (_options.TransientFailureReasons.Contains(ex.Reason))
            {
                throw new TransientServiceBusException(ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Abandons a received message, returning it to the queue.</summary>
    public async Task AbandonMessageAsync(
        ServiceBusReceivedMessage message,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                await _inner.AbandonMessageAsync(message, propertiesToModify, ct)
                    .WaitAsync(ct).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (_options.TransientFailureReasons.Contains(ex.Reason))
            {
                throw new TransientServiceBusException(ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Dead-letters a received message.</summary>
    public async Task DeadLetterMessageAsync(
        ServiceBusReceivedMessage message,
        string? deadLetterReason = null,
        string? deadLetterErrorDescription = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                await _inner.DeadLetterMessageAsync(message, deadLetterReason,
                    deadLetterErrorDescription, ct).WaitAsync(ct).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (_options.TransientFailureReasons.Contains(ex.Reason))
            {
                throw new TransientServiceBusException(ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
