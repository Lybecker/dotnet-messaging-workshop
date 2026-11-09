using Contoso.Messaging.Contracts;
using Exercise3;
using Rebus.Bus;
using Rebus.Exceptions;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Retry.Simple;

public class ChargePaymentHandler(FakePaymentGateway gateway, IBus bus) : IHandleMessages<ChargePayment>
{
    private static readonly SemaphoreSlim ChargeLock = new(1, 1);

    public async Task Handle(ChargePayment cmd)
    {
        // STRETCH: serialize check + charge in this process. Production needs a gateway idempotency key.
        await ChargeLock.WaitAsync();
        try
        {
            if (gateway.HasCharged(cmd.OrderId))
            {
                Console.WriteLine($"  = duplicate for {cmd.OrderId} ignored");
                return;
            }

            try
            {
                var reference = await gateway.ChargeAsync(cmd);                                 // 2
                await bus.Publish(new PaymentCharged(cmd.OrderId, reference, cmd.Amount));
            }
            catch (CardDeclinedException ex)                                                   // 4: business outcome
            {
                await bus.Publish(new PaymentFailed(cmd.OrderId, ex.Message, IsPermanent: true));
            }
            // GatewayTimeoutException and UnsupportedCurrencyException bubble up → retries → IFailed<T>
        }
        finally
        {
            ChargeLock.Release();
        }
    }
}

/// <summary>3b: second-level retries – decide per error type once the quick attempts are spent.</summary>
public class ChargePaymentFailedHandler(IBus bus, IMessageContext context) : IHandleMessages<IFailed<ChargePayment>>
{
    public async Task Handle(IFailed<ChargePayment> failed)
    {
        if (failed.Exceptions.Any(e => e.Type.Contains(nameof(UnsupportedCurrencyException))))
        {
            await bus.Advanced.TransportMessage.Deadletter($"Poison message: {failed.ErrorDescription}");
            return;
        }

        var deferrals = int.TryParse(context.Headers.GetValueOrDefault(Headers.DeferCount), out var n) ? n : 0;
        if (deferrals >= 3)
        {
            await bus.Advanced.TransportMessage.Deadletter($"Gave up after {deferrals} delayed retries: {failed.ErrorDescription}");
            return;
        }

        var delay = TimeSpan.FromSeconds(2 * Math.Pow(2, deferrals));                         // 2 s, 4 s, 8 s
        Console.WriteLine($"  … {failed.Message.OrderId}: deferring {delay.TotalSeconds}s (deferral {deferrals + 1})");
        await bus.Advanced.TransportMessage.Defer(delay);
    }
}

public class PaymentOutcomeHandler : IHandleMessages<PaymentCharged>, IHandleMessages<PaymentFailed>
{
    public Task Handle(PaymentCharged e) { Console.WriteLine($"  ✓ PaymentCharged {e.OrderId} ({e.PaymentReference})"); return Task.CompletedTask; }
    public Task Handle(PaymentFailed e)  { Console.WriteLine($"  ✗ PaymentFailed  {e.OrderId}: {e.Reason}"); return Task.CompletedTask; }
}
