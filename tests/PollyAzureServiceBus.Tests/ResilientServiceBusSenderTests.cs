namespace PollyAzureServiceBus.Tests;

public class ResilientServiceBusSenderTests
{
    // ── Success path ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_Success_CompletesWithoutException()
    {
        var inner = Substitute.For<ServiceBusSender>();
        inner.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        var sender = new ResilientServiceBusSender(inner, TestFactory.FastOptions());
        await sender.SendMessageAsync(TestFactory.MakeMessage());

        await inner.Received(1).SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessagesAsync_Success_CompletesWithoutException()
    {
        var inner = Substitute.For<ServiceBusSender>();
        inner.SendMessagesAsync(Arg.Any<IEnumerable<ServiceBusMessage>>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        var sender = new ResilientServiceBusSender(inner, TestFactory.FastOptions());
        await sender.SendMessagesAsync(new[] { TestFactory.MakeMessage() });

        await inner.Received(1).SendMessagesAsync(Arg.Any<IEnumerable<ServiceBusMessage>>(), Arg.Any<CancellationToken>());
    }

    // ── Retry on transient reasons ────────────────────────────────────────

    [Theory]
    [InlineData(ServiceBusFailureReason.ServiceCommunicationProblem)]
    [InlineData(ServiceBusFailureReason.ServiceTimeout)]
    [InlineData(ServiceBusFailureReason.ServiceBusy)]
    [InlineData(ServiceBusFailureReason.GeneralError)]
    public async Task SendMessageAsync_TransientError_Retries(ServiceBusFailureReason reason)
    {
        int calls = 0;
        var inner = Substitute.For<ServiceBusSender>();
        inner.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
             .Returns(_ =>
             {
                 calls++;
                 if (calls < 2) throw TestFactory.MakeTransientException(reason);
                 return Task.CompletedTask;
             });

        var sender = new ResilientServiceBusSender(inner,
            TestFactory.FastOptions(o => { o.MaxRetries = 3; o.CircuitBreakerMinimumThroughput = 100; }));

        await sender.SendMessageAsync(TestFactory.MakeMessage());
        calls.Should().Be(2);
    }

    [Fact]
    public async Task SendMessageAsync_NonTransientError_NotRetried()
    {
        int calls = 0;
        var inner = Substitute.For<ServiceBusSender>();
        inner.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
             .Returns(_ =>
             {
                 calls++;
                 throw TestFactory.MakeNonTransientException();
                 return Task.CompletedTask;
             });

        var sender = new ResilientServiceBusSender(inner,
            TestFactory.FastOptions(o => { o.MaxRetries = 3; o.CircuitBreakerMinimumThroughput = 100; }));

        var act = () => sender.SendMessageAsync(TestFactory.MakeMessage());
        await act.Should().ThrowAsync<ServiceBusException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task SendMessageAsync_ExhaustsRetries_ThrowsTransientServiceBusException()
    {
        var inner = Substitute.For<ServiceBusSender>();
        inner.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
             .Returns(_ => throw TestFactory.MakeTransientException(ServiceBusFailureReason.ServiceBusy));

        var sender = new ResilientServiceBusSender(inner,
            TestFactory.FastOptions(o => { o.MaxRetries = 2; o.CircuitBreakerMinimumThroughput = 100; }));

        var act = () => sender.SendMessageAsync(TestFactory.MakeMessage());
        await act.Should().ThrowAsync<TransientServiceBusException>()
            .Where(e => e.Reason == ServiceBusFailureReason.ServiceBusy);
    }

    // ── Circuit breaker ───────────────────────────────────────────────────

    [Fact]
    public async Task CircuitBreaker_OpensAfterThreshold()
    {
        var inner = Substitute.For<ServiceBusSender>();
        inner.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
             .Returns(_ => throw TestFactory.MakeTransientException());

        var sender = new ResilientServiceBusSender(inner, TestFactory.FastOptions(o =>
        {
            o.MaxRetries = 0;
            o.CircuitBreakerMinimumThroughput = 3;
            o.CircuitBreakerFailureRatio = 0.5;
            o.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10);
            o.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(10);
        }));

        var exceptions = new List<Exception>();
        for (int i = 0; i < 10; i++)
        {
            try { await sender.SendMessageAsync(TestFactory.MakeMessage()); }
            catch (Exception ex) { exceptions.Add(ex); }
        }

        exceptions.Should().Contain(e => e is BrokenCircuitException);
    }

    // ── Timeout ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_Timeout_ThrowsTimeoutRejectedException()
    {
        var inner = Substitute.For<ServiceBusSender>();
        inner.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
             .Returns(async _ => { await Task.Delay(TimeSpan.FromSeconds(5)); });

        var sender = new ResilientServiceBusSender(inner, new PollyServiceBusOptions
        {
            MaxRetries = 0,
            OperationTimeout = TimeSpan.FromMilliseconds(50),
            CircuitBreakerMinimumThroughput = 100,
        });

        var act = () => sender.SendMessageAsync(TestFactory.MakeMessage());
        await act.Should().ThrowAsync<TimeoutRejectedException>();
    }

    // ── ScheduleMessage ───────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleMessageAsync_Success_ReturnsSequenceNumber()
    {
        var inner = Substitute.For<ServiceBusSender>();
        inner.ScheduleMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(42L));

        var sender = new ResilientServiceBusSender(inner, TestFactory.FastOptions());
        var seq = await sender.ScheduleMessageAsync(TestFactory.MakeMessage(), DateTimeOffset.UtcNow.AddMinutes(5));

        seq.Should().Be(42L);
    }

    // ── Null guards ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullSender_Throws()
    {
        Action act = () => new ResilientServiceBusSender(null!, new PollyServiceBusOptions());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var inner = Substitute.For<ServiceBusSender>();
        Action act = () => new ResilientServiceBusSender(inner, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendMessageAsync_NullMessage_Throws()
    {
        var inner = Substitute.For<ServiceBusSender>();
        var sender = new ResilientServiceBusSender(inner, TestFactory.FastOptions());
        var act = () => sender.SendMessageAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── TransientServiceBusException properties ───────────────────────────

    [Fact]
    public async Task TransientException_HasCorrectProperties()
    {
        var inner = Substitute.For<ServiceBusSender>();
        inner.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
             .Returns(_ => throw TestFactory.MakeTransientException(ServiceBusFailureReason.ServiceTimeout));

        var sender = new ResilientServiceBusSender(inner,
            TestFactory.FastOptions(o => { o.MaxRetries = 0; o.CircuitBreakerMinimumThroughput = 100; }));

        var act = () => sender.SendMessageAsync(TestFactory.MakeMessage());
        var ex = await act.Should().ThrowAsync<TransientServiceBusException>();
        ex.Which.Reason.Should().Be(ServiceBusFailureReason.ServiceTimeout);
        ex.Which.ServiceBusException.Should().NotBeNull();
    }
}
