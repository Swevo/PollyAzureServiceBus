namespace PollyAzureServiceBus.Tests;

internal static class TestFactory
{
    public static PollyServiceBusOptions FastOptions(Action<PollyServiceBusOptions>? configure = null)
    {
        var opts = new PollyServiceBusOptions
        {
            BaseDelay = TimeSpan.Zero,
            MaxDelay = TimeSpan.Zero,
            OperationTimeout = TimeSpan.FromSeconds(10),
            CircuitBreakerMinimumThroughput = 100,
            CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10),
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(1),
        };
        configure?.Invoke(opts);
        return opts;
    }

    public static ServiceBusException MakeTransientException(
        ServiceBusFailureReason reason = ServiceBusFailureReason.ServiceBusy)
        => new ServiceBusException("transient error", reason, "test-queue");

    public static ServiceBusException MakeNonTransientException()
        => new ServiceBusException("not found", ServiceBusFailureReason.MessagingEntityNotFound, "test-queue");

    public static ServiceBusMessage MakeMessage(string body = "hello")
        => new ServiceBusMessage(body);
}
