namespace PollyAzureServiceBus;

/// <summary>
/// Internal helper that constructs the Polly v8 resilience pipeline.
/// </summary>
internal static class PipelineBuilder
{
    public static ResiliencePipeline Build(PollyServiceBusOptions options)
    {
        var predicate = new PredicateBuilder().Handle<TransientServiceBusException>();
        var builder = new ResiliencePipelineBuilder();

        if (options.MaxRetries >= 1)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = predicate,
                MaxRetryAttempts = options.MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = options.BaseDelay,
                MaxDelay = options.MaxDelay,
            });
        }

        builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = predicate,
                FailureRatio = options.CircuitBreakerFailureRatio,
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                SamplingDuration = options.CircuitBreakerSamplingDuration,
                BreakDuration = options.CircuitBreakerBreakDuration,
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.OperationTimeout,
            });

        return builder.Build();
    }
}
