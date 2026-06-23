namespace PollyAzureServiceBus;

/// <summary>
/// Extension methods for <see cref="ServiceBusClient"/> and <see cref="IServiceCollection"/>
/// to create resilient Azure Service Bus senders and receivers.
/// </summary>
public static class ServiceBusExtensions
{
    /// <summary>
    /// Creates a <see cref="ResilientServiceBusSender"/> for the specified queue or topic.
    /// </summary>
    public static ResilientServiceBusSender CreateResilientSender(
        this ServiceBusClient client,
        string queueOrTopicName,
        Action<PollyServiceBusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueOrTopicName);

        var options = new PollyServiceBusOptions();
        configure?.Invoke(options);
        return new ResilientServiceBusSender(client.CreateSender(queueOrTopicName), options);
    }

    /// <summary>
    /// Creates a <see cref="ResilientServiceBusReceiver"/> for the specified queue or subscription.
    /// </summary>
    public static ResilientServiceBusReceiver CreateResilientReceiver(
        this ServiceBusClient client,
        string queueName,
        Action<PollyServiceBusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var options = new PollyServiceBusOptions();
        configure?.Invoke(options);
        return new ResilientServiceBusReceiver(client.CreateReceiver(queueName), options);
    }

    /// <summary>
    /// Creates a <see cref="ResilientServiceBusReceiver"/> for the specified topic subscription.
    /// </summary>
    public static ResilientServiceBusReceiver CreateResilientReceiver(
        this ServiceBusClient client,
        string topicName,
        string subscriptionName,
        Action<PollyServiceBusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

        var options = new PollyServiceBusOptions();
        configure?.Invoke(options);
        return new ResilientServiceBusReceiver(
            client.CreateReceiver(topicName, subscriptionName), options);
    }

    // ── DI registration ──────────────────────────────────────────────────

    /// <summary>
    /// Registers a <see cref="ResilientServiceBusSender"/> as a singleton in the DI container.
    /// </summary>
    public static IServiceCollection AddResilientServiceBusSender(
        this IServiceCollection services,
        string connectionString,
        string queueOrTopicName,
        Action<PollyServiceBusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueOrTopicName);

        return services.AddSingleton(_ =>
        {
            var client = new ServiceBusClient(connectionString);
            return client.CreateResilientSender(queueOrTopicName, configure);
        });
    }

    /// <summary>
    /// Registers a <see cref="ResilientServiceBusReceiver"/> as a singleton in the DI container.
    /// </summary>
    public static IServiceCollection AddResilientServiceBusReceiver(
        this IServiceCollection services,
        string connectionString,
        string queueName,
        Action<PollyServiceBusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        return services.AddSingleton(_ =>
        {
            var client = new ServiceBusClient(connectionString);
            return client.CreateResilientReceiver(queueName, configure);
        });
    }
}
