using Contoso.Messaging.Contracts;
using Contoso.Messaging.Demo;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace Exercise2;

/// <summary>Menu 2 · the Shipping process: owns the shipping queue and handles ShipOrder.</summary>
public static class Shipping
{
    public static async Task RunAsync(Broker broker)
    {
        using var host = Plumbing.BuildHost(broker, "ex2-shipping", opts =>
            {
                Plumbing.CommandQueue<ShipOrder>(opts, broker, Plumbing.ShippingQueue, listen: true);    // 3a: we own the queue
                Plumbing.Event<OrderShipped>(opts, broker, Plumbing.ShippingEvents);                     // 3b: we publish the event
            },
            typeof(ShipOrderHandler));
        await host.StartAsync();
        await Plumbing.WaitForEnter($"Shipping: listening on '{Plumbing.ShippingQueue}'");
        await host.StopAsync();
    }
}

public static class ShipOrderHandler
{
    // 3c: the returned event is published after the handler succeeds (cascading message)
    public static OrderShipped Handle(ShipOrder cmd)
    {
        Console.WriteLine($"Shipping {cmd.OrderId.Short()} to {cmd.ShipTo.City}");
        return new OrderShipped(cmd.OrderId, $"TRK-{cmd.OrderId.Short()}");
    }
}
