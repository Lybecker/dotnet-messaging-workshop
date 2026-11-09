namespace Contoso.Messaging.Reliability;

/// <summary>
/// Exponential back-off with "full jitter" – avoids thundering herds when many consumers
/// retry against the same recovering dependency.
/// delay = random(0, min(cap, base × 2^attempt))
/// </summary>
public static class Backoff
{
    public static TimeSpan Next(int attempt, TimeSpan? @base = null, TimeSpan? cap = null)
    {
        var b = @base ?? TimeSpan.FromSeconds(1);
        var c = cap ?? TimeSpan.FromMinutes(5);
        var exp = Math.Min(c.TotalMilliseconds, b.TotalMilliseconds * Math.Pow(2, attempt));
        return TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * exp);
    }
}

/// <summary>
/// Retry tiers to teach:
///  1. In-process immediate retries (ms)            – blips; keep the lock, 2–3 tries
///  2. Delayed redelivery / second-level retries (s–min) – release the message, retry later with back-off
///  3. Dead-letter + alert (human / re-drive tool)   – after the budget is spent or on permanent errors
/// Always bound by BOTH attempts and total time (message TTL / business deadline).
/// </summary>
public sealed record RetryBudget(int ImmediateRetries = 2, int DelayedRetries = 5, TimeSpan? GiveUpAfter = null);
