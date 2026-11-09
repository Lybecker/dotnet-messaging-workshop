using Microsoft.Data.SqlClient;
using Rebus.Pipeline;
using Rebus.SqlServer.Outbox;

namespace M4;

/// <summary>
/// Inside a handler, the Rebus outbox has already opened a SQL connection and transaction. Outgoing messages are
/// written to the outbox table in that transaction. Use the SAME connection and transaction for your own writes,
/// so "the change + the inbox row + the outgoing event" commit together or not at all.
/// </summary>
public static class SameTransaction
{
    public static (SqlConnection Connection, SqlTransaction Transaction) Of(IMessageContext context)
    {
        var outbox = (OutboxConnection)context.TransactionContext.Items["current-outbox-connection"];
        return (outbox.Connection, outbox.Transaction);
    }

    /// <summary>The inbox: one row per handled MessageId. A duplicate hits the primary key → false.</summary>
    public static async Task<bool> TryRecordInInboxAsync(this IMessageContext context, string messageId, string consumer)
    {
        var (conn, tx) = Of(context);
        await using var cmd = new SqlCommand("INSERT INTO dbo.Inbox (MessageId, Consumer) VALUES (@id, @consumer)", conn, tx);
        cmd.Parameters.AddWithValue("@id", messageId);
        cmd.Parameters.AddWithValue("@consumer", consumer);
        try { await cmd.ExecuteNonQueryAsync(); return true; }
        catch (SqlException ex) when (ex.Number is 2627 or 2601) { return false; }   // PK violation → already handled
    }
}
