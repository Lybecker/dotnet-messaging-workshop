using System.Collections.Concurrent;
using Contoso.Messaging.Contracts;

namespace Exercise3;

// Three kinds of failure – deciding what to do with each one IS the exercise.
public sealed class GatewayTimeoutException(string message) : Exception(message);        // transient → retry
public sealed class CardDeclinedException(string message) : Exception(message);          // business outcome → PaymentFailed event
public sealed class UnsupportedCurrencyException(string message) : Exception(message);   // permanent / poison → error queue, no retries

/// <summary>
/// A deliberately unreliable payment gateway. Do NOT change this class – change how you call it.
///   * ~40% of calls time out (transient)
///   * amounts over 1000 are declined (business rule)
///   * currencies other than EUR / DKK blow up every time (poison message)
/// It also counts charges per OrderId so you can SEE a double charge.
/// </summary>
public sealed class FakePaymentGateway
{
    private readonly ConcurrentDictionary<Guid, int> _charges = new();
    public double TimeoutRate { get; set; } = 0.4;

    public bool HasCharged(Guid orderId) => _charges.ContainsKey(orderId);

    public async Task<string> ChargeAsync(ChargePayment cmd, CancellationToken ct = default)
    {
        await Task.Delay(50, ct);
        if (cmd.Currency is not ("EUR" or "DKK"))
            throw new UnsupportedCurrencyException($"Currency '{cmd.Currency}' is not supported");
        if (Random.Shared.NextDouble() < TimeoutRate)
            throw new GatewayTimeoutException("Payment gateway timed out");
        if (cmd.Amount > 1000m)
            throw new CardDeclinedException($"Card declined: {cmd.Amount} is over the limit");

        var count = _charges.AddOrUpdate(cmd.OrderId, 1, (_, c) => c + 1);
        Console.ForegroundColor = count > 1 ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine(count > 1
            ? $"  !! DOUBLE CHARGE: order {cmd.OrderId} charged {count} times"
            : $"  charged {cmd.Amount} {cmd.Currency} for order {cmd.OrderId}");
        Console.ResetColor();
        return $"PAY-{cmd.OrderId.ToString()[..8]}";
    }
}
