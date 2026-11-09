using Contoso.Messaging.Contracts;
using Contoso.Messaging.Demo;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;

namespace M2.Rebus;

/// <summary>
/// Menu 2 · the Payments process. It owns the payments queue, handles ChargePayment and PUBLISHES PaymentCharged
/// to every subscriber (Service Bus: a topic per event type; RabbitMQ: a topic exchange). The handler only
/// returns: Rebus completes / acks the message for you.
/// </summary>
public static class Payments
{
    public static async Task RunAsync(Broker broker)
    {
        using var host = RebusHost.Build(broker, Queues.Payments, c => c,
            services: s => s.AddRebusHandler<ChargePaymentHandler>());
        await host.StartAsync();
        await RebusHost.WaitForEnter($"Payments: listening on '{Queues.Payments}'");
        await host.StopAsync();
    }
}

public class ChargePaymentHandler(IBus bus) : IHandleMessages<ChargePayment>
{
    public async Task Handle(ChargePayment cmd)
    {
        Console.WriteLine($"Charging {cmd.OrderId}");
        await bus.Publish(new PaymentCharged(cmd.OrderId, "PAY-1", cmd.Amount));
    }
}
