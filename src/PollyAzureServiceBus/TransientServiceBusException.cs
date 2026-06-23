namespace PollyAzureServiceBus;

/// <summary>
/// Thrown when a <see cref="ServiceBusException"/> with a transient
/// <see cref="ServiceBusFailureReason"/> is caught, allowing Polly to identify
/// retriable errors without coupling the pipeline predicate to the Azure SDK.
/// </summary>
public sealed class TransientServiceBusException : Exception
{
    /// <summary>The original <see cref="ServiceBusException"/>.</summary>
    public ServiceBusException ServiceBusException { get; }

    /// <summary>The failure reason.</summary>
    public ServiceBusFailureReason Reason => ServiceBusException.Reason;

    /// <inheritdoc />
    public TransientServiceBusException(ServiceBusException inner)
        : base(inner.Message, inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ServiceBusException = inner;
    }
}
