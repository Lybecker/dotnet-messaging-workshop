using Contoso.Messaging.Demo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Config;

namespace Exercise2;

// ALREADY DONE FOR YOU – no need to change this file.

/// <summary>
/// Connects to the broker in broker.json (repository root) or `dotnet run -- asb | rabbit`, and builds one host
/// per process. The transport line below is the ONLY broker-specific code: your TODOs are the same on both brokers.
/// </summary>
public static class Plumbing
{
    public const string OrdersQueue = "ex2-orders-rebus";       // the Orders process
    public const string ShippingQueue = "ex2-shipping-rebus";   // the Shipping process owns this queue

    public static IHost BuildHost(
        Broker broker,
        string inputQueue,
        Func<RebusConfigurer, RebusConfigurer> configure,
        Action<IServiceCollection> handlers,
        Func<IBus, Task>? onCreated = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        handlers(builder.Services);

        builder.Services.AddRebus(
            configure: c => configure(c.Transport(t =>
            {
                switch (broker)
                {
                    case Broker.ServiceBus:
                        if (DemoEnvironment.UseAzure)
                            t.UseAzureServiceBus($"Endpoint=sb://{DemoEnvironment.ServiceBusNamespace}/", inputQueue, DemoEnvironment.Credential);
                        else
                            t.UseAzureServiceBus(DemoEnvironment.EmulatorConnectionString, inputQueue);
                        break;
                    case Broker.RabbitMq:   t.UseRabbitMq(DemoEnvironment.RabbitMqUri, inputQueue); break;
                }
            })),
            onCreated: onCreated);

        return builder.Build();
    }

    public static Task WaitForEnter(string what)
    {
        Console.WriteLine($"{what} – press ENTER to stop");
        return Task.Run(Console.ReadLine);
    }
}
