using System.Net;
using System.Text.Json;
using Contoso.Messaging.Contracts.Versioning;

namespace Contoso.Messaging.Reliability;

/// <summary>A failure that will never succeed on retry (validation, business rule, unknown version).</summary>
public class BusinessRuleViolationException(string message) : Exception(message);

/// <summary>
/// TRANSIENT vs PERMANENT – the single most important decision in a consumer.
///   Transient  (retry with back-off): timeouts, 429/503, deadlocks, broker throttling, lock lost.
///   Permanent  (dead-letter now):    deserialization errors, validation, unknown message version,
///                                    404 on a resource that will never exist, authorization failures.
/// Default for unknown exceptions: treat as transient but with a bounded number of attempts,
/// so a misclassified permanent error still ends up in the DLQ.
/// </summary>
public static class ErrorClassifier
{
    public static bool IsPermanent(Exception ex) => ex switch
    {
        BusinessRuleViolationException => true,
        UnknownMessageVersionException => true,
        JsonException => true,                         // poison message: body can't be read
        FormatException => true,
        ArgumentException => true,
        HttpRequestException { StatusCode: HttpStatusCode.BadRequest or HttpStatusCode.NotFound
                               or HttpStatusCode.Forbidden or HttpStatusCode.UnprocessableEntity } => true,
        _ => false
    };

    public static bool IsTransient(Exception ex) => !IsPermanent(ex);
}
