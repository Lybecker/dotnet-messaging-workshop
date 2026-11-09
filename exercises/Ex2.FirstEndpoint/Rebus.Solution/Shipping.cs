using Contoso.Messaging.Contracts;
using Contoso.Messaging.Demo;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;

namespace Exercise2;

/// <summary>Menu 2 · the Shipping process: owns the shipping queue and handles ShipOrder.</summary>
public static class Shipping
{
    public static async Task RunAsync(Broker broker)
    {
        using var host = Plumbing.BuildHost(broker, Plumbing.ShippingQueue, c => c,
            handlers: s => s.AddRebusHandler<ShipOrderHandler>());
        await host.StartAsync();
        await Plumbing.WaitForEnter($"Shipping: listening on '{Plumbing.ShippingQueue}'");
        await host.StopAsync();
    }
}

public class ShipOrderHandler(IBus bus) : IHandleMessages<ShipOrder>
{
    public async Task Handle(ShipOrder cmd)
    {
        Console.WriteLine($"Shipping {cmd.OrderId.Short()} to {cmd.ShipTo.City}");
        await bus.Publish(new OrderShipped(cmd.OrderId, $"TRK-{cmd.OrderId.Short()}"));   // 3: Publish = event
    }
}
