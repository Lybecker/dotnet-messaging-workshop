using Contoso.Messaging.Demo;
using Rebus.Config;
using Rebus.Persistence.InMem;
using Rebus.Timeouts;
using Rebus.Transport;

namespace Exercise3;

/// <summary>
/// Already done for you: the broker comes from broker.json (repository root) or `dotnet run -- asb | rabbit`.
/// This is the ONLY broker-specific code in the Rebus exercise – routing, retries and handlers are the same on both.
/// </summary>
public static class ExerciseTransport
{
    public static void Connect(StandardConfigurer<ITransport> t, Broker broker, string queue)
    {
        switch (broker)
        {
            case Broker.ServiceBus: t.UseAzureServiceBus(DemoEnvironment.EmulatorConnectionString, queue); break;
            case Broker.RabbitMq:   t.UseRabbitMq(DemoEnvironment.RabbitMqUri, queue); break;
        }
    }

    /// <summary>Defer needs a timeout store on RabbitMQ (in-memory is fine here); Service Bus schedules natively.</summary>
    public static void Timeouts(StandardConfigurer<ITimeoutManager> t, Broker broker)
    {
        if (broker == Broker.RabbitMq) t.StoreInMemory();
    }
}
