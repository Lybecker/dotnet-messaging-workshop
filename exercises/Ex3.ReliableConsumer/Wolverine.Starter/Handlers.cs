using Contoso.Messaging.Contracts;
using Exercise3;
using Wolverine;

public static class ChargePaymentHandler
{
    private static readonly SemaphoreSlim ChargeLock = new(1, 1);

    // Wolverine finds this by convention. Extra parameters (gateway, envelope, logger) are injected.
    // Whatever you RETURN is sent as a cascading message after the handler succeeds.
    public static async Task<object?> Handle(ChargePayment cmd, FakePaymentGateway gateway, Envelope envelope)
    {
        Console.WriteLine($"  attempt {envelope.Attempts} for {cmd.OrderId}");

        // TODO 2: charge the card with gateway.ChargeAsync(cmd) and RETURN a PaymentCharged event.
        //
        // TODO 4: a declined card (CardDeclinedException) is a BUSINESS OUTCOME, not an error.
        //         Return PaymentFailed(…, IsPermanent: true) instead of letting Wolverine retry it.
        //
        // Then try the menu: ENTER, d, p, b. What happens to each? Look in the RabbitMQ UI → Queues.
        // STRETCH: use ChargeLock around the HasCharged check and charge; release it in finally.
        await Task.CompletedTask;
        throw new NotImplementedException("TODO 2");
    }
}

// STRETCH: press 'r' – the same command arrives twice. Red "DOUBLE CHARGE"? Make the handler idempotent.
//          Dedup on the BUSINESS key (OrderId) – the two copies have different envelope ids, so the
//          durable inbox (opts.Policies.UseDurableInboxOnAllListeners) would NOT catch this one. Why not?

public static class PaymentOutcomeHandler
{
    public static void Handle(PaymentCharged e) => Console.WriteLine($"  ✓ PaymentCharged {e.OrderId} ({e.PaymentReference})");
    public static void Handle(PaymentFailed e)  => Console.WriteLine($"  ✗ PaymentFailed  {e.OrderId}: {e.Reason}");
}
