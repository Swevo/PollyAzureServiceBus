namespace PollyAzureServiceBus;

/// <summary>
/// A resilient Azure Service Bus sender that wraps <see cref="ServiceBusSender"/>
/// with a Polly v8 pipeline: retry → circuit breaker → timeout.
/// </summary>
public sealed class ResilientServiceBusSender : IAsyncDisposable
{
    private readonly ServiceBusSender _inner;
    private readonly ResiliencePipeline _pipeline;
    private readonly PollyServiceBusOptions _options;

    /// <summary>The entity path this sender targets.</summary>
    public string EntityPath => _inner.EntityPath;

    /// <summary>The fully qualified Service Bus namespace.</summary>
    public string FullyQualifiedNamespace => _inner.FullyQualifiedNamespace;

    /// <summary>Initialises the resilient sender.</summary>
    public ResilientServiceBusSender(ServiceBusSender inner, PollyServiceBusOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);
        _inner = inner;
        _options = options;
        _pipeline = PipelineBuilder.Build(options);
    }

    /// <summary>
    /// Sends a single message with Polly resilience applied.
    /// Transient <see cref="ServiceBusException"/>s are retried automatically.
    /// </summary>
    public async Task SendMessageAsync(
        ServiceBusMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                await _inner.SendMessageAsync(message, ct).WaitAsync(ct).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (_options.TransientFailureReasons.Contains(ex.Reason))
            {
                throw new TransientServiceBusException(ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a batch of messages with Polly resilience applied.
    /// </summary>
    public async Task SendMessagesAsync(
        IEnumerable<ServiceBusMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        await _pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                await _inner.SendMessagesAsync(messages, ct).WaitAsync(ct).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (_options.TransientFailureReasons.Contains(ex.Reason))
            {
                throw new TransientServiceBusException(ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Schedules a message for delivery at a future time.
    /// </summary>
    public async Task<long> ScheduleMessageAsync(
        ServiceBusMessage message,
        DateTimeOffset scheduledEnqueueTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return await _pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                return await _inner.ScheduleMessageAsync(message, scheduledEnqueueTime, ct)
                    .WaitAsync(ct).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (_options.TransientFailureReasons.Contains(ex.Reason))
            {
                throw new TransientServiceBusException(ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels a scheduled message by sequence number.
    /// </summary>
    public async Task CancelScheduledMessageAsync(
        long sequenceNumber,
        CancellationToken cancellationToken = default)
    {
        await _pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                await _inner.CancelScheduledMessageAsync(sequenceNumber, ct)
                    .WaitAsync(ct).ConfigureAwait(false);
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
