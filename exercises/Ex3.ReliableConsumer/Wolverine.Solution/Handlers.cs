using Contoso.Messaging.Contracts;
using Exercise3;
using Wolverine;

public static class ChargePaymentHandler
{
    private static readonly SemaphoreSlim ChargeLock = new(1, 1);

    public static async Task<object?> Handle(ChargePayment cmd, FakePaymentGateway gateway, Envelope envelope)
    {
        Console.WriteLine($"  attempt {envelope.Attempts} for {cmd.OrderId}");

        // STRETCH: serialize check + charge in this process. Production needs a gateway idempotency key.
        await ChargeLock.WaitAsync();
        try
        {
            if (gateway.HasCharged(cmd.OrderId))
            {
                Console.WriteLine($"  = duplicate for {cmd.OrderId} ignored");
                return null;                                                                   // nothing to cascade
            }

            try
            {
                var reference = await gateway.ChargeAsync(cmd);                                // 2
                return new PaymentCharged(cmd.OrderId, reference, cmd.Amount);
            }
            catch (CardDeclinedException ex)                                                  // 4: business outcome
            {
                return new PaymentFailed(cmd.OrderId, ex.Message, IsPermanent: true);
            }
            // GatewayTimeoutException and UnsupportedCurrencyException bubble up → the policies in Program.cs
        }
        finally
        {
            ChargeLock.Release();
        }
    }
}

public static class PaymentOutcomeHandler
{
    public static void Handle(PaymentCharged e) => Console.WriteLine($"  ✓ PaymentCharged {e.OrderId} ({e.PaymentReference})");
    public static void Handle(PaymentFailed e)  => Console.WriteLine($"  ✗ PaymentFailed  {e.OrderId}: {e.Reason}");
}
