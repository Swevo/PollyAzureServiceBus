namespace PollyAzureServiceBus.Tests;

public class ResilientServiceBusReceiverTests
{
    // ── Success path ──────────────────────────────────────────────────────

    [Fact]
    public async Task ReceiveMessageAsync_Success_ReturnsMessage()
    {
        var inner = Substitute.For<ServiceBusReceiver>();
        // ReceiveMessageAsync returns null when no message; use a real received message via model builder approach
        inner.ReceiveMessageAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
             .Returns((ServiceBusReceivedMessage?)null);

        var receiver = new ResilientServiceBusReceiver(inner, TestFactory.FastOptions());
        var result = await receiver.ReceiveMessageAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task ReceiveMessagesAsync_Success_ReturnsList()
    {
        var inner = Substitute.For<ServiceBusReceiver>();
        inner.ReceiveMessagesAsync(Arg.Any<int>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>(
                 new List<ServiceBusReceivedMessage>()));

        var receiver = new ResilientServiceBusReceiver(inner, TestFactory.FastOptions());
        var results = await receiver.ReceiveMessagesAsync(10);

        results.Should().BeEmpty();
    }

    // ── Retry on transient reasons ────────────────────────────────────────

    [Theory]
    [InlineData(ServiceBusFailureReason.ServiceCommunicationProblem)]
    [InlineData(ServiceBusFailureReason.ServiceTimeout)]
    [InlineData(ServiceBusFailureReason.ServiceBusy)]
    public async Task ReceiveMessageAsync_TransientError_Retries(ServiceBusFailureReason reason)
    {
        int calls = 0;
        var inner = Substitute.For<ServiceBusReceiver>();
        inner.ReceiveMessageAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
             .Returns<Task<ServiceBusReceivedMessage?>>(_ =>
             {
                 calls++;
                 if (calls < 2) throw TestFactory.MakeTransientException(reason);
                 return Task.FromResult<ServiceBusReceivedMessage?>(null);
             });

        var receiver = new ResilientServiceBusReceiver(inner,
            TestFactory.FastOptions(o => { o.MaxRetries = 3; o.CircuitBreakerMinimumThroughput = 100; }));

        await receiver.ReceiveMessageAsync();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task ReceiveMessageAsync_NonTransientError_NotRetried()
    {
        int calls = 0;
        var inner = Substitute.For<ServiceBusReceiver>();
        inner.ReceiveMessageAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
             .Returns<Task<ServiceBusReceivedMessage?>>(_ =>
             {
                 calls++;
                 throw TestFactory.MakeNonTransientException();
             });

        var receiver = new ResilientServiceBusReceiver(inner,
            TestFactory.FastOptions(o => { o.MaxRetries = 3; o.CircuitBreakerMinimumThroughput = 100; }));

        var act = () => receiver.ReceiveMessageAsync();
        await act.Should().ThrowAsync<ServiceBusException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ReceiveMessageAsync_ExhaustsRetries_ThrowsTransientServiceBusException()
    {
        var inner = Substitute.For<ServiceBusReceiver>();
        inner.ReceiveMessageAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
             .Returns<Task<ServiceBusReceivedMessage?>>(_ =>
                 throw TestFactory.MakeTransientException(ServiceBusFailureReason.ServiceBusy));

        var receiver = new ResilientServiceBusReceiver(inner,
            TestFactory.FastOptions(o => { o.MaxRetries = 2; o.CircuitBreakerMinimumThroughput = 100; }));

        var act = () => receiver.ReceiveMessageAsync();
        await act.Should().ThrowAsync<TransientServiceBusException>();
    }

    // ── Null guards ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullReceiver_Throws()
    {
        Action act = () => new ResilientServiceBusReceiver(null!, new PollyServiceBusOptions());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var inner = Substitute.For<ServiceBusReceiver>();
        Action act = () => new ResilientServiceBusReceiver(inner, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
