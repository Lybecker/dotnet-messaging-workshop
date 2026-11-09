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
                // TODO 3a: Shipping OWNS the ShipOrder queue, so it listens on it.
                //          Hint: Plumbing.CommandQueue<T>(opts, broker, Plumbing.ShippingQueue, listen: …)
                // TODO 3b: Shipping publishes OrderShipped to Plumbing.ShippingEvents.
                //          Hint: Plumbing.Event<T>(opts, broker, topic)
            },
            typeof(ShipOrderHandler));
        await host.StartAsync();
        await Plumbing.WaitForEnter($"Shipping: listening on '{Plumbing.ShippingQueue}'");
        await host.StopAsync();
    }
}

public static class ShipOrderHandler
{
    // TODO 3c: RETURN an OrderShipped event with a tracking number. Wolverine publishes whatever the handler
    //          returns, after the handler succeeds (a cascading message). Change the return type to OrderShipped.
    public static void Handle(ShipOrder cmd)
    {
        Console.WriteLine($"Shipping {cmd.OrderId.Short()} to {cmd.ShipTo.City}");
    }
}
