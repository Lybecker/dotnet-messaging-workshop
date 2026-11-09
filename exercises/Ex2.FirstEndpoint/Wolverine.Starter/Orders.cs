using Contoso.Messaging.Contracts;
using Contoso.Messaging.Demo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace Exercise2;

/// <summary>Menu 1 · the Orders process: sends ShipOrder and wants to hear about OrderShipped.</summary>
public static class Orders
{
    public static async Task RunAsync(Broker broker)
    {
        using var host = Plumbing.BuildHost(broker, "ex2-orders", opts =>
            {
                // TODO 1: a command has ONE owner. Route ShipOrder to the queue Shipping owns (Plumbing.ShippingQueue).
                //         Orders only sends, so it does not listen on that queue.
                //         Hint: Plumbing.CommandQueue<T>(opts, broker, queue, listen: …)

                // TODO 4: events are only delivered to subscribers. Subscribe to OrderShipped with our own
                //         subscription (Plumbing.OrdersSubscription) on Plumbing.ShippingEvents.
                //         Hint: Plumbing.Event<T>(opts, broker, topic, subscription: …)
            },
            typeof(OrderShippedHandler));
        await host.StartAsync();
        var bus = host.Services.GetRequiredService<IMessageBus>();

        Console.WriteLine("Orders: ENTER = ship an order · q = quit");
        while (Console.ReadLine() is { } line && line != "q")
        {
            var cmd = Sample.NewShipOrder();
            // TODO 2: SEND the command. SendAsync = to its one owner; PublishAsync = to whoever subscribed.
            //         Try it once BEFORE TODO 1: what does Wolverine say when a command has no route?
            Console.WriteLine($"→ ShipOrder {cmd.OrderId.Short()}");
        }
        await host.StopAsync();
    }
}

/// <summary>Already done: found by its name (…Handler) and its Handle method. Prints the event when it arrives.</summary>
public static class OrderShippedHandler
{
    public static void Handle(OrderShipped evt) =>
        Console.WriteLine($"← OrderShipped {evt.OrderId.Short()} ({evt.TrackingNumber})");
}
