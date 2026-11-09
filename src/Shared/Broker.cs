using Microsoft.Extensions.Configuration;

namespace Contoso.Messaging.Demo;

/// <summary>Which broker the framework demos run on.</summary>
public enum Broker { ServiceBus, RabbitMq }

/// <summary>
/// Reads the ONE broker setting, first match wins:
///   1. the command line   dotnet run -- asb | rabbit     (or --Broker asb)
///   2. the environment    BROKER=asb
///   3. broker.json        "Broker": "rabbit"   (repository root, copied next to every framework demo)
///   4. default            rabbit
/// Everything that differs between the brokers lives in each project's Transport.cs – nowhere else.
/// </summary>
public static class BrokerSetting
{
    public static Broker Resolve(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("broker.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args.Where(a => a.StartsWith("--")).ToArray())
            .Build();

        var value = args.FirstOrDefault(a => !a.StartsWith("--")) ?? config["Broker"] ?? "rabbit";
        return value.Trim().ToLowerInvariant() switch
        {
            "asb" or "servicebus" or "azureservicebus" => Broker.ServiceBus,
            "rabbit" or "rabbitmq" => Broker.RabbitMq,
            _ => throw new ArgumentException($"Unknown broker '{value}'. Use 'asb' or 'rabbit' (broker.json, BROKER or the command line).")
        };
    }

    public static string Describe(this Broker broker) => broker switch
    {
        Broker.ServiceBus => DemoEnvironment.UseAzure ? $"Azure Service Bus ({DemoEnvironment.ServiceBusNamespace})" : "Azure Service Bus (emulator)",
        _ => "RabbitMQ"
    };
}
