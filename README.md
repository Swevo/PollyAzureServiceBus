# PollyAzureServiceBus

[![NuGet](https://img.shields.io/nuget/v/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus)
[![Build](https://github.com/Swevo/PollyAzureServiceBus/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyAzureServiceBus/actions/workflows/build.yml)

**Polly v8 resilience for Azure Service Bus** — automatic retry, circuit breaker, and per-operation timeout for sending and receiving messages. Drop-in wrappers for `ServiceBusSender` and `ServiceBusReceiver`, no configuration changes required.

## Why PollyAzureServiceBus?

The Azure Service Bus SDK ships with its own retry policy, but it only covers a subset of transient failures and cannot be composed with your broader application resilience strategy. PollyAzureServiceBus gives you the full power of Polly v8 — exponential back-off with jitter, circuit breaker isolation, per-call timeouts, and telemetry hooks — applied consistently to every send and receive operation.

| Feature | Azure SDK retry | PollyAzureServiceBus |
|---------|:---:|:---:|
| Exponential back-off with jitter | ❌ | ✅ |
| Circuit breaker | ❌ | ✅ |
| Per-operation timeout | ❌ | ✅ |
| Composable with app resilience | ❌ | ✅ |
| DI registration | ❌ | ✅ |
| Targets net8 + net9 | ✅ | ✅ |

## Installation

```bash
dotnet add package PollyAzureServiceBus
```

## Quick Start

### Sender

```csharp
var client = new ServiceBusClient(connectionString);

// Extension method on ServiceBusClient
var sender = client.CreateResilientSender("my-queue", o =>
{
    o.MaxRetries    = 3;
    o.BaseDelay     = TimeSpan.FromMilliseconds(500);
    o.OperationTimeout = TimeSpan.FromSeconds(30);
});

await sender.SendMessageAsync(new ServiceBusMessage("Hello, World!"));
```

### Receiver

```csharp
var receiver = client.CreateResilientReceiver("my-queue");

// or for topic subscriptions:
var receiver = client.CreateResilientReceiver("my-topic", "my-subscription");

var message = await receiver.ReceiveMessageAsync();
if (message is not null)
{
    // process message...
    await receiver.CompleteMessageAsync(message);
}
```

### With Dependency Injection

```csharp
// Program.cs
builder.Services.AddResilientServiceBusSender(
    connectionString: Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION")!,
    queueOrTopicName: "orders",
    configure: o =>
    {
        o.MaxRetries        = 3;
        o.OperationTimeout  = TimeSpan.FromSeconds(30);
    });

builder.Services.AddResilientServiceBusReceiver(
    connectionString: Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION")!,
    queueName: "orders");

// Inject ResilientServiceBusSender / ResilientServiceBusReceiver
```

### With Managed Identity (DefaultAzureCredential)

```csharp
using Azure.Identity;

var credential = new DefaultAzureCredential();
var client = new ServiceBusClient("mynamespace.servicebus.windows.net", credential);
var sender = client.CreateResilientSender("my-queue");
```

## Configuration

```csharp
var options = new PollyServiceBusOptions
{
    // Retry
    MaxRetries = 3,                              // 0 = no retry
    BaseDelay  = TimeSpan.FromMilliseconds(500), // exponential base
    MaxDelay   = TimeSpan.FromSeconds(30),

    // Circuit breaker
    CircuitBreakerFailureRatio      = 0.5,
    CircuitBreakerMinimumThroughput = 10,
    CircuitBreakerSamplingDuration  = TimeSpan.FromSeconds(30),
    CircuitBreakerBreakDuration     = TimeSpan.FromSeconds(5),

    // Timeout
    OperationTimeout = TimeSpan.FromSeconds(30),

    // Which failure reasons are treated as transient (eligible for retry)
    TransientFailureReasons = new HashSet<ServiceBusFailureReason>
    {
        ServiceBusFailureReason.ServiceCommunicationProblem,
        ServiceBusFailureReason.ServiceTimeout,
        ServiceBusFailureReason.ServiceBusy,
        ServiceBusFailureReason.GeneralError,
    },
};
```

| Property | Default | Description |
|---|---|---|
| `MaxRetries` | `3` | Retry attempts (0 = disabled) |
| `BaseDelay` | `500 ms` | Base delay for exponential back-off with jitter |
| `MaxDelay` | `30 s` | Cap for exponential back-off delay |
| `CircuitBreakerFailureRatio` | `0.5` | Failure ratio to open circuit |
| `CircuitBreakerMinimumThroughput` | `10` | Minimum calls before CB can open |
| `CircuitBreakerSamplingDuration` | `30 s` | Sliding window for failure ratio |
| `CircuitBreakerBreakDuration` | `5 s` | How long the circuit stays open |
| `OperationTimeout` | `30 s` | Max time per operation before `TimeoutRejectedException` |
| `TransientFailureReasons` | see above | `ServiceBusFailureReason` set that triggers retry/CB |

## API Reference

### `ResilientServiceBusSender`

| Method | Description |
|---|---|
| `SendMessageAsync(message, ct)` | Send a single message |
| `SendMessagesAsync(messages, ct)` | Send a batch of messages |
| `ScheduleMessageAsync(message, time, ct)` | Schedule a message for future delivery |
| `CancelScheduledMessageAsync(seqNum, ct)` | Cancel a scheduled message |

### `ResilientServiceBusReceiver`

| Method | Description |
|---|---|
| `ReceiveMessageAsync(maxWait, ct)` | Receive a single message (returns `null` on timeout) |
| `ReceiveMessagesAsync(max, maxWait, ct)` | Receive a batch of messages |
| `CompleteMessageAsync(message, ct)` | Complete (delete) a message |
| `AbandonMessageAsync(message, props, ct)` | Return message to queue |
| `DeadLetterMessageAsync(message, reason, desc, ct)` | Move message to dead-letter queue |

## Error Handling

Non-transient `ServiceBusException`s (e.g. `MessagingEntityNotFound`, `MessageSizeExceeded`) are rethrown as-is. After retries are exhausted the last exception is a `TransientServiceBusException` wrapping the original:

```csharp
try
{
    await sender.SendMessageAsync(message);
}
catch (TransientServiceBusException ex)
{
    // All retries failed
    Console.WriteLine($"Service Bus failure: {ex.Reason} — {ex.ServiceBusException.Message}");
}
catch (BrokenCircuitException)
{
    // Circuit is open — fail fast without hitting the broker
}
catch (TimeoutRejectedException)
{
    // Operation exceeded OperationTimeout
}
```

## Resilience Pipeline Order

```
Retry → Circuit Breaker → Timeout → Service Bus operation
```

## Related Packages

| Package | Description |
|---|---|
| [PollyElasticsearch](https://github.com/Swevo/PollyElasticsearch) | Polly v8 for Elastic.Clients.Elasticsearch |
| [PollyAzureKeyVault](https://github.com/Swevo/PollyAzureKeyVault) | Polly v8 for Azure Key Vault |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | Advanced back-off strategies with jitter |
| [PollyChaos](https://www.nuget.org/packages/PollyChaos) | Chaos engineering — inject faults in tests |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | Polly pipeline behaviour for MediatR |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | Resilient EF Core execution strategies |
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | Health check endpoints for Polly circuits |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | Retry + rate-limit handling for OpenAI / Azure OpenAI |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | Resilient StackExchange.Redis wrapper |
| [PollySignalR](https://www.nuget.org/packages/PollySignalR) | Reconnect policy for SignalR `HubConnection` |
| [PollyGrpc](https://www.nuget.org/packages/PollyGrpc) | Polly v8 resilience for gRPC .NET clients |
| [PollyKafka](https://www.nuget.org/packages/PollyKafka) | Polly v8 resilience for Confluent.Kafka producers and consumers |
| [PollyCaching](https://www.nuget.org/packages/PollyCaching) | Distributed cache with Polly resilience |
| [PollyBulkhead](https://www.nuget.org/packages/PollyBulkhead) | Bulkhead isolation for concurrent workloads |

| [PollyRabbitMQ](https://www.nuget.org/packages/PollyRabbitMQ) | Polly v8 resilience for RabbitMQ.Client channels |

## License

MIT
