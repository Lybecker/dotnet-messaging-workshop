using Contoso.Messaging.Reliability;
using M4;
using Rebus.Bus;
using Rebus.Exceptions;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Retry.Simple;

/// <summary>
/// Tier 2 (second-level retries): Rebus calls this when the immediate attempts are used up, or at once for a
/// fail-fast exception. We classify the failure and decide:
///   permanent → publish PaymentFailed (a business outcome Orders can act on) and move the message to the error queue
///   transient → try again later with back-off (Defer), at most 5 times, then the error queue
/// </summary>
public class ChargePaymentFailedHandler(IBus bus, IMessageContext context, ILogger<ChargePaymentFailedHandler> log)
    : IHandleMessages<IFailed<ChargePayment>>
{
    public async Task Handle(IFailed<ChargePayment> failed)
    {
        var cmd = failed.Message;
        var reason = failed.Exceptions.LastOrDefault()?.Message ?? failed.ErrorDescription;
        using var scope = log.BeginScope(new Dictionary<string, object> { ["OrderId"] = cmd.OrderId });

        var permanent = failed.Exceptions.Any(e => e.Type.Contains(nameof(BusinessRuleViolationException)));
        if (permanent)
        {
            log.LogError("Order {OrderId}: permanent failure ({Reason}) → PaymentFailed + error queue", cmd.OrderId, reason);
            await bus.Publish(new PaymentFailed(failed.Message.OrderId, reason));
            await bus.Advanced.TransportMessage.Deadletter(failed.ErrorDescription);
            return;
        }

        var deferrals = int.TryParse(context.Headers.GetValueOrDefault(Headers.DeferCount), out var n) ? n : 0;
        if (deferrals >= 5)
        {
            log.LogError("Order {OrderId}: gave up after {DelayedRetries} delayed retries → error queue", cmd.OrderId, deferrals);
            await bus.Advanced.TransportMessage.Deadletter($"Gave up after {deferrals} delayed retries: {failed.ErrorDescription}");
            return;
        }

        log.LogWarning("Order {OrderId}: transient failure ({Reason}) → delayed retry {DelayedRetry}", cmd.OrderId, reason, deferrals + 1);
        await bus.Advanced.TransportMessage.Defer(Backoff.Next(deferrals, TimeSpan.FromSeconds(5)));
    }
}
