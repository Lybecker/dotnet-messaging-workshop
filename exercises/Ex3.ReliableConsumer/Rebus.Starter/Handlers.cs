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
        // TODO 2: charge the card with gateway.ChargeAsync(cmd) and PUBLISH PaymentCharged.
        //
        // TODO 4: a declined card (CardDeclinedException) is a BUSINESS OUTCOME, not an error.
        //         Publish PaymentFailed(…, IsPermanent: true) instead of letting Rebus retry it.
        //
        // Then try the menu: ENTER, d, p, b. What happens to each? Look in the RabbitMQ UI → Queues.
        // STRETCH: use ChargeLock around the HasCharged check and charge; release it in finally.
        await Task.CompletedTask;
        throw new NotImplementedException("TODO 2");
    }
}

/// <summary>3b: second-level retries – decide per error type once the quick attempts are spent.</summary>
public class ChargePaymentFailedHandler(IBus bus, IMessageContext context) : IHandleMessages<IFailed<ChargePayment>>
{
    public async Task Handle(IFailed<ChargePayment> failed)
    {
        // TODO 3b: dead-letter UnsupportedCurrencyException immediately. Defer transient failures
        //          with back-off (2, 4, 8 seconds), then dead-letter after three deferrals.
        //          Use context.Headers[Headers.DeferCount] to count delays.
        //          The RabbitMQ timeout store is already wired in Program.cs.
        await Task.CompletedTask;
        throw new NotImplementedException("TODO 3b");
    }
}

public class PaymentOutcomeHandler : IHandleMessages<PaymentCharged>, IHandleMessages<PaymentFailed>
{
    public Task Handle(PaymentCharged e) { Console.WriteLine($"  ✓ PaymentCharged {e.OrderId} ({e.PaymentReference})"); return Task.CompletedTask; }
    public Task Handle(PaymentFailed e)  { Console.WriteLine($"  ✗ PaymentFailed  {e.OrderId}: {e.Reason}"); return Task.CompletedTask; }
}
