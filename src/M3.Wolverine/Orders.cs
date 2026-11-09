using Contoso.Messaging.Contracts;
using Contoso.Messaging.Demo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace M3.Wolverine;

/// <summary>
/// Menu 1 · the Orders process.
///   SendAsync → the ChargePayment COMMAND goes to exactly one queue: its owner, Payments
///   Subscribe → Orders has its own subscription (Service Bus) / bound queue (RabbitMQ) for PaymentCharged
/// Routes are declared once per message type (Transport.cs). No ServiceBusMessage, no BasicProperties, no serialiser:
/// Wolverine fills the envelope (id, correlation-id, message-type, content-type). Look in the broker UI.
/// </summary>
public static class Orders
{
    public static async Task RunAsync(Broker broker)
    {
        using var host = WolverineHost.Build(broker, "m3-orders", opts =>
            {
                Transport.CommandQueue<ChargePayment>(opts, broker, Names.Payments, listen: false);   // Payments owns the queue
                Transport.Event<PaymentCharged>(opts, broker, Names.PaymentEvents, subscription: Names.OrdersSubscription);
            },
            typeof(PaymentChargedHandler));
        await host.StartAsync();
        var bus = host.Services.GetRequiredService<IMessageBus>();

        Console.WriteLine("Orders: ENTER = place an order · q = quit");
        while (Console.ReadLine() is { } line && line != "q")
        {
            var orderId = Guid.NewGuid();
            await bus.SendAsync(new ChargePayment(orderId, Guid.NewGuid(), 99m, "EUR"));
            Console.WriteLine($"→ ChargePayment {orderId.ToString()[..8]}");
        }
        await host.StopAsync();
    }
}

public static class PaymentChargedHandler
{
    public static void Handle(PaymentCharged evt) =>
        Console.WriteLine($"← PaymentCharged {evt.OrderId.ToString()[..8]} ({evt.PaymentReference})");
}
