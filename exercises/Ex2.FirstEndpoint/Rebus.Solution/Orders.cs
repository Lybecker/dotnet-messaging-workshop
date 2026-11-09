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
                .Map<ShipOrder>(Plumbing.ShippingQueue)),                      // 1: the command's one owner
            handlers: s => s.AddRebusHandler<OrderShippedHandler>(),
            onCreated: bus => bus.Subscribe<OrderShipped>());                    // 4: topic subscription / exchange binding
        await host.StartAsync();
        var bus = host.Services.GetRequiredService<IBus>();

        Console.WriteLine("Orders: ENTER = ship an order · q = quit");
        while (Console.ReadLine() is { } line && line != "q")
        {
            var cmd = Sample.NewShipOrder();
            await bus.Send(cmd);                                                 // 2: Send = command
            Console.WriteLine($"→ ShipOrder {cmd.OrderId.Short()}");
        }
        await host.StopAsync();
    }
}

public class OrderShippedHandler : IHandleMessages<OrderShipped>
{
    public Task Handle(OrderShipped evt)
    {
        Console.WriteLine($"← OrderShipped {evt.OrderId.Short()} ({evt.TrackingNumber})");
        return Task.CompletedTask;
    }
}
