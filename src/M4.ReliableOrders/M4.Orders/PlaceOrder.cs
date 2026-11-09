using System.Diagnostics;
using M4;
using Microsoft.Data.SqlClient;
using Rebus.Bus;
using Rebus.Config.Outbox;
using Rebus.Messages;
using Rebus.Transport;

/// <summary>Body of POST /orders. The customer name picks the scenario (Messages.cs).</summary>
public record NewOrder(string Customer, decimal Amount = 99m, bool Duplicate = false, bool CrashAfterCommit = false);

/// <summary>
/// The dual-write problem, solved: the order row and the ChargePayment command are written in ONE SQL transaction
/// (the command goes to the outbox table). After the commit, the outbox sends it. If the process dies in between,
/// the command is still in the outbox and is sent when the process starts again. Nothing lost, no ghost orders.
/// </summary>
public static class PlaceOrder
{
    public static async Task<IResult> HandleAsync(NewOrder order, IBus bus, ILogger<NewOrder> log)
    {
        var orderId = Guid.NewGuid();
        Activity.Current?.SetTag("order.id", orderId.ToString());
        using var scope = log.BeginScope(new Dictionary<string, object> { ["OrderId"] = orderId, ["CorrelationId"] = orderId.ToString() });

        await using var conn = new SqlConnection(Database.ConnectionString);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        using var rebus = new RebusTransactionScope();
        rebus.UseOutbox(conn, tx);                                      // messages sent in this scope → outbox table, in tx

        await using (var insert = new SqlCommand("""
            INSERT INTO dbo.Orders (Id, Customer, Amount, Status, UpdatedAt) VALUES (@id, @customer, @amount, 'Pending', sysdatetimeoffset())
            """, conn, tx))
        {
            insert.Parameters.AddWithValue("@id", orderId);
            insert.Parameters.AddWithValue("@customer", order.Customer);
            insert.Parameters.AddWithValue("@amount", order.Amount);
            await insert.ExecuteNonQueryAsync();
        }

        // Metadata: the order id is the correlation id; Rebus copies it to every message this one causes
        var headers = new Dictionary<string, string>
        {
            [Headers.CorrelationId] = orderId.ToString(),
            [Headers.MessageId] = Guid.NewGuid().ToString()
        };
        var command = new ChargePayment(orderId, order.Customer, order.Amount);
        await bus.Send(command, headers);
        if (order.Duplicate)
            await bus.Send(command, new Dictionary<string, string>(headers));   // same MessageId again: what a redelivery looks like

        await rebus.CompleteAsync();
        await tx.CommitAsync();
        log.LogInformation("Order {OrderId} placed by {Customer} for {Amount}: Pending", orderId, order.Customer, order.Amount);

        if (order.CrashAfterCommit)
        {
            log.LogCritical("Order {OrderId} committed – crashing BEFORE the outbox sends ChargePayment. Start orders-api again.", orderId);
            Environment.FailFast("Demo: crash between the database commit and the broker");
        }
        return Results.Accepted($"/orders/{orderId}", new { orderId, status = "Pending" });
    }
}
