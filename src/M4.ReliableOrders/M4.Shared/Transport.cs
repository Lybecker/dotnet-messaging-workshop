using Contoso.Messaging.Demo;
using Microsoft.Extensions.Configuration;
using Rebus.Config;
using Rebus.Timeouts;
using Rebus.Transport;

namespace M4;

/// <summary>
/// THE broker switch for both processes. All Service Bus vs RabbitMQ differences live in this file.
/// Broker: broker.json in the repository root, or per run: dotnet run -- asb | rabbit.
/// </summary>
public static class Transport
{
    public const string OrdersQueue = "m4-orders";
    public const string PaymentsQueue = "m4-payments";
    public const string ErrorQueue = "m4-error";
    public const string ParkedQueue = "m4-error-parked";

    /// <summary>The Service Bus namespace from demo.settings.json (setup.sh) or ASB_NAMESPACE; null = local emulator.</summary>
    public static string? ServiceBusNamespace(IConfiguration config) =>
        config["ServiceBus:Namespace"] ?? DemoEnvironment.ServiceBusNamespace;

    public static void Connect(StandardConfigurer<ITransport> t, Broker broker, IConfiguration config, string inputQueue)
    {
        switch (broker)
        {
            case Broker.ServiceBus:
                var ns = ServiceBusNamespace(config);
                var asb = ns is null
                    ? t.UseAzureServiceBus(DemoEnvironment.EmulatorConnectionString, inputQueue)
                    : t.UseAzureServiceBus($"Endpoint=sb://{ns}/", inputQueue, DemoEnvironment.Credential);   // Entra ID, no keys
                // A crashed consumer's message comes back when its lock expires: 30 s instead of minutes for the demo
                asb.SetMessagePeekLockDuration(TimeSpan.FromSeconds(30));
                break;

            case Broker.RabbitMq:
                t.UseRabbitMq(DemoEnvironment.RabbitMqUri, inputQueue);   // a crashed consumer's message comes back at once
                break;
        }
    }

    /// <summary>Delayed retries (Defer): native scheduled messages on Service Bus, a timeout table on RabbitMQ.</summary>
    public static void Timeouts(StandardConfigurer<ITimeoutManager> t, Broker broker)
    {
        if (broker == Broker.RabbitMq) t.StoreInSqlServer(DemoEnvironment.SqlConnectionString, "RebusTimeouts");
    }

    public static string Describe(Broker broker, IConfiguration config) => broker switch
    {
        Broker.ServiceBus => ServiceBusNamespace(config) is { } ns ? $"Azure Service Bus ({ns})" : "Azure Service Bus (emulator)",
        _ => "RabbitMQ"
    };
}
