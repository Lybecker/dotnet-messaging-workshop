using System.Diagnostics;
using M4;
using Microsoft.Data.SqlClient;
using Rebus.Handlers;

// The order's status follows the events: Pending → Paid or Failed (eventual consistency).
// UPDATE … WHERE Id = @id is naturally idempotent: handling the same event twice changes nothing.

public class PaymentChargedHandler(ILogger<PaymentChargedHandler> log) : IHandleMessages<PaymentCharged>
{
    public async Task Handle(PaymentCharged evt)
    {
        await OrderStatus.SetAsync(evt.OrderId, "Paid");
        log.LogInformation("Order {OrderId} is Paid ({PaymentReference})", evt.OrderId, evt.PaymentReference);
    }
}

public class PaymentFailedHandler(ILogger<PaymentFailedHandler> log) : IHandleMessages<PaymentFailed>
{
    public async Task Handle(PaymentFailed evt)
    {
        await OrderStatus.SetAsync(evt.OrderId, "Failed");
        log.LogWarning("Order {OrderId} is Failed: {Reason}", evt.OrderId, evt.Reason);
    }
}

public static class OrderStatus
{
    public static async Task SetAsync(Guid orderId, string status)
    {
        Activity.Current?.SetTag("order.id", orderId.ToString());
        await using var conn = new SqlConnection(Database.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.Orders SET Status = @s, UpdatedAt = sysdatetimeoffset() WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@s", status);
        cmd.Parameters.AddWithValue("@id", orderId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>GET /orders and the 's' key: the 10 most recent orders.</summary>
    public static async Task<IResult> ListAsync() => Results.Ok(await LatestAsync());

    public static async Task<List<(Guid Id, string Customer, string Status)>> LatestAsync()
    {
        var list = new List<(Guid, string, string)>();
        await using var conn = new SqlConnection(Database.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT TOP 10 Id, Customer, Status FROM dbo.Orders ORDER BY UpdatedAt DESC", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add((r.GetGuid(0), r.GetString(1), r.GetString(2)));
        return list;
    }
}
