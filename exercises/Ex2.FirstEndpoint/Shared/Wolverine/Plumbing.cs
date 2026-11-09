using Contoso.Messaging.Demo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

namespace Exercise2;

// ALREADY DONE FOR YOU – no need to change this file. You call CommandQueue and Event from your TODOs.

/// <summary>
/// Connects to the broker in broker.json (repository root) or `dotnet run -- asb | rabbit`, and builds one host
/// per process. This is the ONLY broker-specific code: your TODOs are the same on both brokers.
/// </summary>
public static class Plumbing
{
    public const string ShippingQueue = "ex2-shipping-wolverine";              // the Shipping process owns this queue
    public const string ShippingEvents = "ex2-shipping-events-wolverine";      // Shipping publishes OrderShipped here
    public const string OrdersSubscription = "ex2-orders-wolverine";           // the Orders process's own subscription

    private static RabbitMqTransportExpression? _rabbit;

    /// <summary>One host with ONLY the handler types you pass in, so the two processes don't share handlers.</summary>
    public static IHost BuildHost(Broker broker, string serviceName, Action<WolverineOptions> configure, params Type[] handlers)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.UseWolverine(opts =>
        {
            opts.ServiceName = serviceName;
            switch (broker)
            {
                case Broker.ServiceBus: opts.UseAzureServiceBusEmulator().AutoProvision(); break;
                case Broker.RabbitMq:   _rabbit = opts.UseRabbitMq(new Uri(DemoEnvironment.RabbitMqUri)).AutoProvision(); break;
            }

            opts.Discovery.DisableConventionalDiscovery();
            foreach (var handler in handlers) opts.Discovery.IncludeType(handler);

            configure(opts);
        });
        return builder.Build();
    }

    /// <summary>A command has one owner: send T to <paramref name="queue"/>. listen: true only in the process that owns it.</summary>
    public static void CommandQueue<T>(WolverineOptions opts, Broker broker, string queue, bool listen)
    {
        switch (broker)
        {
            case Broker.ServiceBus:
                opts.PublishMessage<T>().ToAzureServiceBusQueue(queue);
                if (listen) opts.ListenToAzureServiceBusQueue(queue);
                break;
            case Broker.RabbitMq:
                opts.PublishMessage<T>().ToRabbitQueue(queue);
                if (listen) opts.ListenToRabbitQueue(queue);
                break;
        }
    }

    /// <summary>An event goes to a topic (Service Bus) / exchange (RabbitMQ). Pass a subscription name to receive it too.</summary>
    public static void Event<T>(WolverineOptions opts, Broker broker, string topic, string? subscription = null)
    {
        switch (broker)
        {
            case Broker.ServiceBus:
                opts.PublishMessage<T>().ToAzureServiceBusTopic(topic);
                if (subscription is not null) opts.ListenToAzureServiceBusSubscription(subscription).FromTopic(topic);
                break;
            case Broker.RabbitMq:
                opts.PublishMessage<T>().ToRabbitExchange(topic);
                if (subscription is not null)
                {
                    _rabbit!.DeclareExchange(topic, ex => ex.BindQueue(subscription));
                    opts.ListenToRabbitQueue(subscription);
                }
                break;
        }
    }

    public static Task WaitForEnter(string what)
    {
        Console.WriteLine($"{what} – press ENTER to stop");
        return Task.Run(Console.ReadLine);
    }
}
