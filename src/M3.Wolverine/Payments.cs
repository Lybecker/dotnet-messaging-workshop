using Contoso.Messaging.Contracts;
using Contoso.Messaging.Demo;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace M3.Wolverine;

/// <summary>
/// Menu 2 · the Payments process. It owns the payments queue and handles ChargePayment. The handler RETURNS
/// PaymentCharged: Wolverine publishes the returned message after the handler succeeds (a "cascading message"),
/// and completes / acks the incoming one. No IMessageBus needed, so the handler is easy to unit test.
/// </summary>
public static class Payments
{
    public static async Task RunAsync(Broker broker)
    {
        using var host = WolverineHost.Build(broker, "m3-payments", opts =>
            {
                Transport.CommandQueue<ChargePayment>(opts, broker, Names.Payments);   // we own the queue: listen on it
                Transport.Event<PaymentCharged>(opts, broker, Names.PaymentEvents);    // we publish the event
            },
            typeof(ChargePaymentHandler));
        await host.StartAsync();
        await WolverineHost.WaitForEnter($"Payments: listening on '{Names.Payments}'");
        await host.StopAsync();
    }
}

// Found by convention: a class named …Handler with a Handle method. Metadata is on the Envelope.
public class ChargePaymentHandler
{
    public static PaymentCharged Handle(ChargePayment cmd, Envelope envelope)
    {
        Console.WriteLine($"Charging {cmd.OrderId}, correlation {envelope.CorrelationId}");
        // the returned event is published after the handler succeeds
        return new PaymentCharged(cmd.OrderId, $"PAY-{cmd.OrderId.ToString()[..8]}", cmd.Amount);
    }
}
