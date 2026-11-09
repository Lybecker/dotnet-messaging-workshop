using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace Contoso.Messaging.Reliability;

/// <summary>
/// IDEMPOTENT CONSUMER (a.k.a. inbox / processed-messages table).
/// At-least-once delivery means every handler WILL see duplicates (redelivery after lock loss,
/// producer retries outside the dedup window, re-drives from the DLQ). Options, best first:
///   1. Naturally idempotent operations (SET status = 'Paid' WHERE id = @id, upserts)
///   2. Business-key checks (is order X already paid?)
///   3. Inbox table keyed on MessageId, written in the SAME transaction as the state change
/// </summary>
public interface IInbox
{
    Task<bool> TryBeginAsync(string messageId, CancellationToken ct = default);
    Task MarkProcessedAsync(string messageId, CancellationToken ct = default);
    Task ReleaseAsync(string messageId);
}

public sealed class InMemoryInbox : IInbox
{
    private readonly ConcurrentDictionary<string, bool> _seen = new();
    public Task<bool> TryBeginAsync(string id, CancellationToken ct = default) => Task.FromResult(_seen.TryAdd(id, false));
    public Task MarkProcessedAsync(string id, CancellationToken ct = default) { _seen[id] = true; return Task.CompletedTask; }
    public Task ReleaseAsync(string id) { if (_seen.TryGetValue(id, out var done) && !done) _seen.TryRemove(id, out _); return Task.CompletedTask; }
}

/// <summary>
/// The production version: insert into the inbox and apply the business change in ONE
/// database transaction. A duplicate hits the primary key and is skipped.
///   CREATE TABLE dbo.Inbox (MessageId nvarchar(200) NOT NULL PRIMARY KEY,
///                           Consumer  nvarchar(100) NOT NULL,
///                           ProcessedAt datetimeoffset NOT NULL DEFAULT sysdatetimeoffset());
/// Clean up rows older than (max redelivery horizon + DLQ retention).
/// </summary>
public sealed class SqlIdempotentHandler(string connectionString, string consumerName)
{
    public async Task<bool> HandleOnceAsync(string messageId, Func<SqlConnection, SqlTransaction, Task> businessChange, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            await using (var cmd = new SqlCommand(
                "INSERT INTO dbo.Inbox (MessageId, Consumer) VALUES (@id, @consumer)", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", messageId);
                cmd.Parameters.AddWithValue("@consumer", consumerName);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await businessChange(conn, tx);
            await tx.CommitAsync(ct);
            return true;                                        // processed now
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601) // PK violation → duplicate
        {
            await tx.RollbackAsync(ct);
            return false;                                       // already processed → just ack
        }
    }
}
