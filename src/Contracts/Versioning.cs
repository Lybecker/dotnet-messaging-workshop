using System.Text.Json;
using System.Text.Json.Nodes;

namespace Contoso.Messaging.Contracts.Versioning;

// ─────────────────────────────────────────────────────────────────────────────
// EVENT VERSIONING – the rules we teach:
//  1. Additive, optional changes are NON-breaking: add a field with a default. Never remove,
//     rename or change the meaning/type of a field in place.
//  2. Consumers are TOLERANT READERS: ignore unknown fields, don't fail on missing optional ones.
//  3. Breaking change = NEW message type ("...order-placed.v2"). Publish v1 AND v2 in parallel for
//     a deprecation window, track who still consumes v1, then retire v1.
//  4. Version lives in metadata (ce-type / schema-version), not only in the CLR type name.
//  5. Consumers may UPCAST old versions to the newest internal model at the edge.
// ─────────────────────────────────────────────────────────────────────────────

// v1 – original contract (still published during the deprecation window)
public sealed record OrderPlacedV1(Guid OrderId, Guid CustomerId, decimal Total);

// v2 – NON-breaking evolution of v1 would be: add `string? Currency = null`.
// v2 here is BREAKING: Total became Money (amount + currency) => new type + new ce-type.
public sealed record Money(decimal Amount, string Currency);
public sealed record OrderPlacedV2(Guid OrderId, Guid CustomerId, Money Total, string SalesChannel = "web");

public static class OrderPlacedTypes
{
    public const string V1 = "contoso.orders.order-placed.v1";
    public const string V2 = "contoso.orders.order-placed.v2";
}

/// <summary>Consumer-side upcaster: everything is converted to the latest model at the edge.</summary>
public static class OrderPlacedUpcaster
{
    private static readonly JsonSerializerOptions Tolerant = new(JsonSerializerDefaults.Web)
    {
        // Tolerant reader: unknown members are ignored (the System.Text.Json default),
        // we only make it explicit for the training.
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
    };

    public static OrderPlacedV2 ToLatest(string ceType, ReadOnlySpan<byte> body) => ceType switch
    {
        OrderPlacedTypes.V2 => JsonSerializer.Deserialize<OrderPlacedV2>(body, Tolerant)!,
        OrderPlacedTypes.V1 => Upcast(JsonSerializer.Deserialize<OrderPlacedV1>(body, Tolerant)!),
        _ => throw new UnknownMessageVersionException(ceType)   // permanent failure → dead-letter
    };

    private static OrderPlacedV2 Upcast(OrderPlacedV1 v1) =>
        new(v1.OrderId, v1.CustomerId, new Money(v1.Total, "EUR" /* documented v1 default */), SalesChannel: "unknown");
}

public sealed class UnknownMessageVersionException(string type)
    : Exception($"No handler/upcaster for message type '{type}'.");

/// <summary>
/// Publisher-side: during the deprecation window publish both versions.
/// Track consumers of v1 (subscription metrics / broker bindings) to know when you can stop.
/// </summary>
public static class DualPublish
{
    public static IEnumerable<(string ceType, object payload)> For(OrderPlacedV2 e)
    {
        yield return (OrderPlacedTypes.V2, e);
        yield return (OrderPlacedTypes.V1, new OrderPlacedV1(e.OrderId, e.CustomerId, e.Total.Amount));
    }
}
