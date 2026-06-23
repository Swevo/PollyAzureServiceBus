namespace PollyAzureServiceBus.Tests;

public class ServiceBusExtensionsTests
{
    private const string FakeConnString =
        "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    // ── CreateResilientSender ─────────────────────────────────────────────

    [Fact]
    public void CreateResilientSender_NullClient_Throws()
    {
        ServiceBusClient? client = null;
        Action act = () => client!.CreateResilientSender("my-queue");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateResilientSender_EmptyQueue_Throws()
    {
        var client = new ServiceBusClient(FakeConnString);
        Action act = () => client.CreateResilientSender("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateResilientSender_ValidArgs_ReturnsSender()
    {
        var client = new ServiceBusClient(FakeConnString);
        var sender = client.CreateResilientSender("my-queue");
        sender.Should().NotBeNull();
        sender.EntityPath.Should().Be("my-queue");
    }

    [Fact]
    public void CreateResilientSender_WithOptions_AppliesOptions()
    {
        var client = new ServiceBusClient(FakeConnString);
        var sender = client.CreateResilientSender("my-queue", o => o.MaxRetries = 5);
        sender.Should().NotBeNull();
    }

    // ── CreateResilientReceiver ───────────────────────────────────────────

    [Fact]
    public void CreateResilientReceiver_NullClient_Throws()
    {
        ServiceBusClient? client = null;
        Action act = () => client!.CreateResilientReceiver("my-queue");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateResilientReceiver_EmptyQueue_Throws()
    {
        var client = new ServiceBusClient(FakeConnString);
        Action act = () => client.CreateResilientReceiver("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateResilientReceiver_ValidArgs_ReturnsReceiver()
    {
        var client = new ServiceBusClient(FakeConnString);
        var receiver = client.CreateResilientReceiver("my-queue");
        receiver.Should().NotBeNull();
        receiver.EntityPath.Should().Be("my-queue");
    }

    [Fact]
    public void CreateResilientReceiver_TopicSubscription_ReturnsReceiver()
    {
        var client = new ServiceBusClient(FakeConnString);
        var receiver = client.CreateResilientReceiver("my-topic", "my-subscription");
        receiver.Should().NotBeNull();
    }

    // ── DI registration ───────────────────────────────────────────────────

    [Fact]
    public void AddResilientServiceBusSender_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Action act = () => services!.AddResilientServiceBusSender(FakeConnString, "my-queue");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddResilientServiceBusSender_RegistersSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddResilientServiceBusSender(FakeConnString, "my-queue");
        services.Should().Contain(d => d.ServiceType == typeof(ResilientServiceBusSender));
    }

    [Fact]
    public void AddResilientServiceBusReceiver_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Action act = () => services!.AddResilientServiceBusReceiver(FakeConnString, "my-queue");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddResilientServiceBusReceiver_RegistersSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddResilientServiceBusReceiver(FakeConnString, "my-queue");
        services.Should().Contain(d => d.ServiceType == typeof(ResilientServiceBusReceiver));
    }
}
