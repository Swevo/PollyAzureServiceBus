namespace PollyAzureServiceBus;

/// <summary>
/// Configuration options for Polly resilience applied to Azure Service Bus operations.
/// </summary>
public sealed class PollyServiceBusOptions
{
    // ── Retry ─────────────────────────────────────────────────────────────

    /// <summary>Number of retry attempts. Set to 0 to disable retries.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base delay for exponential back-off with jitter.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Maximum delay cap for exponential back-off.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    // ── Circuit breaker ───────────────────────────────────────────────────

    /// <summary>Fraction of failures (0–1) required to open the circuit.</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>Minimum calls within the sampling window before the circuit can open.</summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>Sliding window over which failures are measured.</summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How long the circuit stays open before moving to half-open.</summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(5);

    // ── Timeout ───────────────────────────────────────────────────────────

    /// <summary>Maximum time allowed per send or receive operation.</summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    // ── Transient failure reasons ─────────────────────────────────────────

    /// <summary>
    /// <see cref="ServiceBusFailureReason"/> values treated as transient (eligible for retry).
    /// Defaults to the standard transient reasons defined by the Azure SDK.
    /// </summary>
    public HashSet<ServiceBusFailureReason> TransientFailureReasons { get; set; } = new()
    {
        ServiceBusFailureReason.ServiceCommunicationProblem,
        ServiceBusFailureReason.ServiceTimeout,
        ServiceBusFailureReason.ServiceBusy,
        ServiceBusFailureReason.GeneralError,
    };
}
