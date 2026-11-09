using System.Collections.Concurrent;
using System.Diagnostics;
using M4;
using Microsoft.Data.SqlClient;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

/// <summary>
/// Charges an order. Three things happen in ONE SQL transaction (the Rebus outbox's):
///   1. inbox row for this MessageId  → a duplicate is skipped (at-least-once delivery, re-drives, producer retries)
///   2. the payment row               → the business change
///   3. PaymentCharged                → written to the outbox, sent only after the commit
/// Throwing rolls all three back, and Rebus retries.
/// </summary>
public class ChargePaymentHandler(IBus bus, IMessageContext context, ILogger<ChargePaymentHandler> log) : IHandleMessages<ChargePayment>
{
    private static readonly ConcurrentDictionary<string, int> Attempts = new();

    public async Task Handle(ChargePayment cmd)
    {
        var messageId = context.Headers[Headers.MessageId];
        var delayedRetries = int.TryParse(context.Headers.GetValueOrDefault(Headers.DeferCount), out var d) ? d : 0;
        var attempt = Attempts.AddOrUpdate(messageId, 1, (_, n) => n + 1);

        // Business id on the span and on every log line → searchable in Application Insights
        Activity.Current?.SetTag("order.id", cmd.OrderId.ToString());
        using var scope = log.BeginScope(new Dictionary<string, object>
        {
            ["OrderId"] = cmd.OrderId, ["MessageId"] = messageId, ["Attempt"] = attempt, ["DelayedRetries"] = delayedRetries,
            ["CorrelationId"] = context.Headers.GetValueOrDefault(Headers.CorrelationId) ?? ""
        });
        log.LogInformation("Charging {Amount} for order {OrderId} ({Customer}), attempt {Attempt}", cmd.Amount, cmd.OrderId, cmd.Customer, attempt);

        // 1. Inbox: have we handled this message before?
        if (!await context.TryRecordInInboxAsync(messageId, "payments"))
        {
            log.LogWarning("Duplicate message {MessageId} for order {OrderId}: already charged, skipped", messageId, cmd.OrderId);
            return;                                                         // ack it, do nothing
        }

        await PaymentGateway.ChargeAsync(cmd, delayedRetries, log);        // may throw: transient or permanent

        // 2. The business change, in the same transaction
        var (conn, tx) = SameTransaction.Of(context);
        var reference = $"PAY-{cmd.OrderId.ToString()[..8]}";
        await using (var insert = new SqlCommand(
            "INSERT INTO dbo.Payments (OrderId, Reference, Amount) VALUES (@id, @ref, @amount)", conn, tx))
        {
            insert.Parameters.AddWithValue("@id", cmd.OrderId);
            insert.Parameters.AddWithValue("@ref", reference);
            insert.Parameters.AddWithValue("@amount", cmd.Amount);
            await insert.ExecuteNonQueryAsync();
        }

        // 3. The event, through the outbox
        await bus.Publish(new PaymentCharged(cmd.OrderId, reference, cmd.Amount));
        log.LogInformation($"Charged {cmd.Amount} for order {cmd.OrderId}");                       // BAD: just a string
        log.LogInformation("Charged {Amount} for order {OrderId} as {PaymentReference}",           // GOOD: queryable columns
            cmd.Amount, cmd.OrderId, reference);
    }
}
