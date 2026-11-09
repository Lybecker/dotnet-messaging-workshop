using Contoso.Messaging.Demo;
using Rebus.Config;
using Rebus.Transport;

namespace M2.Rebus;

/// <summary>
/// THE broker switch for the Rebus demo. All Service Bus vs RabbitMQ differences live in this file: the transport line.
/// Routing (Map&lt;T&gt;), Send/Publish/Subscribe and handlers are identical on both brokers.
/// Pick the broker in broker.json (repository root) or per run: dotnet run -- asb | rabbit.
/// </summary>
public static class Transport
{
    public static void Connect(StandardConfigurer<ITransport> t, Broker broker, string inputQueue)
    {
        switch (broker)
        {
            case Broker.ServiceBus:
                if (DemoEnvironment.UseAzure)
                    // Entra ID: endpoint-only connection string + TokenCredential (managed identity / az login).
                    // Rebus creates topics/subscriptions per event type → needs "Azure Service Bus Data Owner".
                    t.UseAzureServiceBus($"Endpoint=sb://{DemoEnvironment.ServiceBusNamespace}/", inputQueue, DemoEnvironment.Credential);
                else
                    t.UseAzureServiceBus(DemoEnvironment.EmulatorConnectionString, inputQueue);
                break;

            case Broker.RabbitMq:
                t.UseRabbitMq(DemoEnvironment.RabbitMqUri, inputQueue);
                break;
        }
    }
}
