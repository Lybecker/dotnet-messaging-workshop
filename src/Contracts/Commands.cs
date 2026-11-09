namespace Contoso.Messaging.Contracts;

// ─────────────────────────────────────────────────────────────────────────────
// COMMANDS — "please do X".
//  * Imperative name (verb + noun).
//  * Exactly ONE logical receiver that owns the behaviour (sent to a queue).
//  * The receiver may refuse; the sender usually cares about the outcome.
//  * Owned by the RECEIVER: the receiving team publishes the contract.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record PlaceOrder(Guid OrderId, Guid CustomerId, IReadOnlyList<OrderLine> Lines);

public sealed record ChargePayment(Guid OrderId, Guid CustomerId, decimal Amount, string Currency);

public sealed record RefundPayment(Guid OrderId, string Reason);        // compensating command

public sealed record ReserveStock(Guid OrderId, IReadOnlyList<OrderLine> Lines);

public sealed record ReleaseStock(Guid OrderId, string Reason);         // compensating command

public sealed record ShipOrder(Guid OrderId, Address ShipTo);

public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);

public sealed record Address(string Street, string City, string PostalCode, string CountryCode);
