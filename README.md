# PollyAzureServiceBus

[![NuGet](https://img.shields.io/nuget/v/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus)
[![Build](https://github.com/Swevo/PollyAzureServiceBus/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyAzureServiceBus/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

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

| Package | Downloads | Description |
|---|---|---|
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers — expose circuit-breaker state (Closed, HalfOpen, Open, Isolated) as /health endpoint responses |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Backoff delay strategies for Polly v8 resilience pipelines |
| [PollyGrpc](https://www.nuget.org/packages/PollyGrpc) | [![Downloads](https://img.shields.io/nuget/dt/PollyGrpc.svg)](https://www.nuget.org/packages/PollyGrpc) | Polly v8 resilience interceptor for gRPC |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | [![Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience pipelines for Entity Framework Core — wrap every EF Core query and SaveChanges with retry, timeout and circuit-breaker via a single AddPollyResilience() call |
| [PollyRabbitMQ](https://www.nuget.org/packages/PollyRabbitMQ) | [![Downloads](https://img.shields.io/nuget/dt/PollyRabbitMQ.svg)](https://www.nuget.org/packages/PollyRabbitMQ) | Polly v8 resilience for RabbitMQ.Client v7+ — retry, circuit-breaker, and timeout for IChannel operations, with built-in RabbitMqTransientErrors predicate covering AlreadyClosedException, BrokerUnreachableException, OperationInterruptedException, and ConnectFailureException |
| [PollyMailKit](https://www.nuget.org/packages/PollyMailKit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMailKit.svg)](https://www.nuget.org/packages/PollyMailKit) | Polly v8 resilience pipelines for MailKit — retry, timeout, and circuit-breaker for SmtpClient.SendAsync and any MailKit SMTP operation |
| [PollyMassTransit](https://www.nuget.org/packages/PollyMassTransit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMassTransit.svg)](https://www.nuget.org/packages/PollyMassTransit) | Polly v8 resilience pipelines for MassTransit — retry, timeout, and circuit-breaker for IBus.Publish and ISendEndpointProvider.Send |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI API calls |
| [PollyAzureEventHub](https://www.nuget.org/packages/PollyAzureEventHub) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureEventHub.svg)](https://www.nuget.org/packages/PollyAzureEventHub) | Polly v8 resilience pipelines for Azure Event Hubs — retry, timeout, and circuit-breaker for EventHubProducerClient and EventHubConsumerClient |
| [PollySignalR](https://www.nuget.org/packages/PollySignalR) | [![Downloads](https://img.shields.io/nuget/dt/PollySignalR.svg)](https://www.nuget.org/packages/PollySignalR) | Polly v8 reconnect policy for SignalR |
| [PollyElasticsearch](https://www.nuget.org/packages/PollyElasticsearch) | [![Downloads](https://img.shields.io/nuget/dt/PollyElasticsearch.svg)](https://www.nuget.org/packages/PollyElasticsearch) | Polly v8 resilience pipelines for Elastic.Clients.Elasticsearch 8+ — retry, timeout, and circuit-breaker for any Elasticsearch operation, plus a built-in ElasticTransientErrors predicate covering rate limiting (429), service unavailability (503), gateway timeouts (504), and connection failures |
| [PollyHangfire](https://www.nuget.org/packages/PollyHangfire) | [![Downloads](https://img.shields.io/nuget/dt/PollyHangfire.svg)](https://www.nuget.org/packages/PollyHangfire) | Polly v8 resilience pipelines for Hangfire — retry, timeout, and circuit-breaker for IBackgroundJobClient.Enqueue and Schedule |
| [PollySendGrid](https://www.nuget.org/packages/PollySendGrid) | [![Downloads](https://img.shields.io/nuget/dt/PollySendGrid.svg)](https://www.nuget.org/packages/PollySendGrid) | Polly v8 resilience pipelines for SendGrid — retry, timeout, and circuit-breaker for ISendGridClient.SendEmailAsync |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | [![Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR) | Polly v8 resilience pipelines for MediatR — add retry, timeout, circuit-breaker, rate-limiting, hedging, and chaos engineering to any MediatR request handler with a single line of DI registration |
| [PollyAzureKeyVault](https://www.nuget.org/packages/PollyAzureKeyVault) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureKeyVault.svg)](https://www.nuget.org/packages/PollyAzureKeyVault) | Polly v8 resilience pipelines for Azure Key Vault — retry, timeout, and circuit-breaker for SecretClient, KeyClient, and CertificateClient |
| [PollyAzureQueueStorage](https://www.nuget.org/packages/PollyAzureQueueStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureQueueStorage.svg)](https://www.nuget.org/packages/PollyAzureQueueStorage) | Polly v8 resilience pipelines for Azure Queue Storage — retry, timeout, and circuit-breaker for Azure.Storage.Queues QueueClient |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | [![Downloads](https://img.shields.io/nuget/dt/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis) | Polly v8 resilience for StackExchange.Redis |
| [PollyKafka](https://www.nuget.org/packages/PollyKafka) | [![Downloads](https://img.shields.io/nuget/dt/PollyKafka.svg)](https://www.nuget.org/packages/PollyKafka) | Polly v8 resilience for Confluent.Kafka — retry, circuit breaker, and timeout for producers and consumers |
| [PollyAzureTableStorage](https://www.nuget.org/packages/PollyAzureTableStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureTableStorage.svg)](https://www.nuget.org/packages/PollyAzureTableStorage) | Polly v8 resilience pipelines for Azure Table Storage — retry, timeout, and circuit-breaker for Azure.Data.Tables TableClient |
| [PollyCaching](https://www.nuget.org/packages/PollyCaching) | [![Downloads](https://img.shields.io/nuget/dt/PollyCaching.svg)](https://www.nuget.org/packages/PollyCaching) | A caching resilience strategy for Polly v8 pipelines |
| [PollyChaos](https://www.nuget.org/packages/PollyChaos) | [![Downloads](https://img.shields.io/nuget/dt/PollyChaos.svg)](https://www.nuget.org/packages/PollyChaos) | Chaos engineering and fault-injection resilience strategies for Polly v8 pipelines |
| [PollyBulkhead](https://www.nuget.org/packages/PollyBulkhead) | [![Downloads](https://img.shields.io/nuget/dt/PollyBulkhead.svg)](https://www.nuget.org/packages/PollyBulkhead) | Bulkhead isolation strategy for Polly v8 resilience pipelines |

## 💼 Need .NET consulting?

The author of this package is available for consulting on **Polly v8 resilience**, **Azure cloud architecture**, and **clean .NET design**.

**[→ solidqualitysolutions.com](https://www.solidqualitysolutions.com/)** · **[LinkedIn](https://www.linkedin.com/in/justbannister/)**
## License

MIT
