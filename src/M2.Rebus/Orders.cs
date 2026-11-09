using Contoso.Messaging.Contracts;
using Contoso.Messaging.Demo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Routing.TypeBased;

namespace M2.Rebus;

/// <summary>
/// Menu 1 · the Orders process.
///   Send      → the ChargePayment COMMAND goes to exactly one queue: its owner, Payments (routing: type → queue)
///   Subscribe → Orders gets every PaymentCharged EVENT that Payments publishes
/// No ServiceBusMessage, no BasicProperties, no serialiser: Rebus adds the headers (rbs2-msg-id, rbs2-corr-id,
/// rbs2-msg-type) and the JSON body. Look at them in Service Bus Explorer or the RabbitMQ UI.
/// </summary>
public static class Orders
{
    public static async Task RunAsync(Broker broker)
    {
        using var host = RebusHost.Build(broker, Queues.Orders,
            c => c.Routing(r => r.TypeBased().Map<ChargePayment>(Queues.Payments)),
            services: s => s.AddRebusHandler<PaymentChargedHandler>(),
            onCreated: bus => bus.Subscribe<PaymentCharged>());
        await host.StartAsync();
        var bus = host.Services.GetRequiredService<IBus>();

        Console.WriteLine("Orders: ENTER = place an order · q = quit");
        while (Console.ReadLine() is { } line && line != "q")
        {
            var orderId = Guid.NewGuid();
            await bus.Send(new ChargePayment(orderId, Guid.NewGuid(), 99m, "EUR"));
            Console.WriteLine($"→ ChargePayment {orderId.ToString()[..8]}");
        }
        await host.StopAsync();
    }
}

public class PaymentChargedHandler : IHandleMessages<PaymentCharged>
{
    public Task Handle(PaymentCharged evt)
    {
        Console.WriteLine($"← PaymentCharged {evt.OrderId.ToString()[..8]} ({evt.PaymentReference})");
        return Task.CompletedTask;
    }
}
