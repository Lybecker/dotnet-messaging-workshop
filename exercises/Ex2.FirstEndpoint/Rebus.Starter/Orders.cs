using Contoso.Messaging.Contracts;
using Contoso.Messaging.Demo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Routing.TypeBased;

namespace Exercise2;

/// <summary>Menu 1 · the Orders process: sends ShipOrder and wants to hear about OrderShipped.</summary>
public static class Orders
{
    public static async Task RunAsync(Broker broker)
    {
        using var host = Plumbing.BuildHost(broker, Plumbing.OrdersQueue,
            c => c.Routing(r => r.TypeBased()
                // TODO 1: a command has ONE owner. Route ShipOrder to the queue Shipping owns (Plumbing.ShippingQueue).
                //         Hint: .Map<T>(queue)
            ),
            handlers: s => s.AddRebusHandler<OrderShippedHandler>(),
            onCreated: async bus =>
            {
                // TODO 4: events are only delivered to subscribers. Subscribe to OrderShipped.
                await Task.CompletedTask;
            });
        await host.StartAsync();
        var bus = host.Services.GetRequiredService<IBus>();

        Console.WriteLine("Orders: ENTER = ship an order · q = quit");
        while (Console.ReadLine() is { } line && line != "q")
        {
            var cmd = Sample.NewShipOrder();
            // TODO 2: SEND the command. Send = to its one owner; Publish = to whoever subscribed.
            //         Try it once BEFORE TODO 1: what does Rebus say when a command has no route?
            Console.WriteLine($"→ ShipOrder {cmd.OrderId.Short()}");
        }
        await host.StopAsync();
    }
}

/// <summary>Already done: prints the event when it arrives (after TODO 3 and 4).</summary>
public class OrderShippedHandler : IHandleMessages<OrderShipped>
{
    public Task Handle(OrderShipped evt)
    {
        Console.WriteLine($"← OrderShipped {evt.OrderId.Short()} ({evt.TrackingNumber})");
        return Task.CompletedTask;
    }
}
