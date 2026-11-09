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
                Plumbing.CommandQueue<ShipOrder>(opts, broker, Plumbing.ShippingQueue, listen: false);   // 1: Shipping owns it
                Plumbing.Event<OrderShipped>(opts, broker, Plumbing.ShippingEvents,
                    subscription: Plumbing.OrdersSubscription);                                        // 4: our own subscription
            },
            typeof(OrderShippedHandler));
        await host.StartAsync();
        var bus = host.Services.GetRequiredService<IMessageBus>();

        Console.WriteLine("Orders: ENTER = ship an order · q = quit");
        while (Console.ReadLine() is { } line && line != "q")
        {
            var cmd = Sample.NewShipOrder();
            await bus.SendAsync(cmd);                                                                 // 2: Send = command
            Console.WriteLine($"→ ShipOrder {cmd.OrderId.Short()}");
        }
        await host.StopAsync();
    }
}

public static class OrderShippedHandler
{
    public static void Handle(OrderShipped evt) =>
        Console.WriteLine($"← OrderShipped {evt.OrderId.Short()} ({evt.TrackingNumber})");
}
