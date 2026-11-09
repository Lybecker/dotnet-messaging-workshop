namespace Contoso.Messaging.Contracts;

// ─────────────────────────────────────────────────────────────────────────────
// EVENTS — "X has happened".
//  * Past-tense name. A fact; it cannot be rejected, only reacted to.
//  * ZERO..N subscribers the publisher does not know about (published to a topic/exchange).
//  * Owned by the PUBLISHER: the publishing team owns and versions the contract.
//
// DOMAIN event  = in-process, inside one bounded context (never leaves the service).
// INTEGRATION event = crosses a service boundary, goes through the broker, is a public,
//                     versioned contract (everything in this file).
// ─────────────────────────────────────────────────────────────────────────────

// FAT (event-carried state transfer): consumers get everything they need, no call-back.
//  + autonomy, consumers keep working if Orders is down
//  − bigger payload, more coupling to the publisher's schema, PII spreads, harder to version
public sealed record OrderPlaced(
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    string Currency,
    Address ShipTo,
    DateTimeOffset PlacedAt);

// SLIM (event notification): "something changed, ask me if you care".
//  + tiny, stable contract, no PII on the wire
//  − consumers call back (runtime coupling, load on the publisher, possible stale reads:
//    always fetch "as of version N", never "latest")
public sealed record OrderChanged(Guid OrderId, long Version, string ChangeType);

public sealed record PaymentCharged(Guid OrderId, string PaymentReference, decimal Amount);
public sealed record PaymentFailed(Guid OrderId, string Reason, bool IsPermanent);
public sealed record PaymentRefunded(Guid OrderId);

public sealed record StockReserved(Guid OrderId);
public sealed record StockReservationFailed(Guid OrderId, string Reason);
public sealed record StockReleased(Guid OrderId);

public sealed record OrderShipped(Guid OrderId, string TrackingNumber);
public sealed record OrderCancelled(Guid OrderId, string Reason);
public sealed record OrderCompleted(Guid OrderId);
