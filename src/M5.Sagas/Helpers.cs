namespace M5.Sagas;

/// <summary>Demo stand-in for a card gateway: declines the orders it has been told to decline.</summary>
public static class Cards
{
    static readonly HashSet<Guid> Declined = [];
    public static void Decline(Guid orderId) { lock (Declined) Declined.Add(orderId); }
    public static bool TryCharge(Guid orderId) { lock (Declined) return !Declined.Contains(orderId); }
}

/// <summary>Demo stand-in for the Inventory service's own database.</summary>
public static class Stock
{
    public static void Reserve(Guid orderId) => Console.WriteLine($"  [inventory] reserve stock for {Short(orderId)}");
    public static void Release(Guid orderId) => Console.WriteLine($"  [inventory] COMPENSATE: release stock for {Short(orderId)}");
    public static string Short(Guid id) => id.ToString()[..8];
}
