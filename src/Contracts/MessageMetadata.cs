namespace Contoso.Messaging.Contracts;

/// <summary>
/// The metadata every message in the estate should carry, independent of broker.
/// Map these to native properties where they exist (ASB: MessageId, CorrelationId, Subject,
/// ContentType; AMQP 0-9-1: message_id, correlation_id, type, content_type) and to
/// application properties / headers for the rest. Names follow CloudEvents where possible.
/// </summary>
public static class MessageHeaders
{
    public const string MessageType    = "ce-type";          // e.g. "contoso.orders.order-placed.v2"
    public const string Source         = "ce-source";        // e.g. "/contoso/orders-api"
    public const string SchemaVersion  = "schema-version";   // "2"
    public const string CausationId    = "causation-id";     // MessageId of the message that caused this one
    public const string TraceParent    = "traceparent";      // W3C trace context (OpenTelemetry)
    public const string TenantId       = "tenant-id";
    public const string OccurredAt     = "ce-time";
}

/// <summary>
/// Rules of thumb for IDs (show this on the "Correlation IDs" slide):
///  * MessageId     – unique per message, DETERMINISTIC when you want broker/consumer dedup
///                    (e.g. $"{orderId}:{nameof(ChargePayment)}"), never a fresh Guid on retry.
///  * CorrelationId – the business conversation / workflow (usually the OrderId) – copied to every
///                    message caused by the first one.
///  * CausationId   – MessageId of the direct parent – lets you rebuild the causal tree.
///  * traceparent   – technical distributed trace; complements, does not replace, CorrelationId.
/// </summary>
public sealed record MessageContext(
    string MessageId,
    string CorrelationId,
    string? CausationId,
    string MessageType,
    string Source,
    DateTimeOffset OccurredAt)
{
    public static MessageContext NewConversation(string messageType, string source, string correlationId) =>
        new(Guid.NewGuid().ToString("N"), correlationId, null, messageType, source, DateTimeOffset.UtcNow);

    /// <summary>Create the context for a message caused by this one.</summary>
    public MessageContext Next(string messageType, string source, string? deterministicId = null) =>
        new(deterministicId ?? Guid.NewGuid().ToString("N"), CorrelationId, MessageId, messageType, source, DateTimeOffset.UtcNow);
}
