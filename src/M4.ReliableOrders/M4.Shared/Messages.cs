namespace M4;

// The customer name picks the scenario in the fake payment gateway (Payments):
//   alice    → charged at once                     flaky   → times out until it has been delayed once (transient)
//   declined → card declined, never retried         slow    → takes 15 s, so you can kill Payments mid-message

/// <summary>Command: Orders → Payments. Exactly one owner: the m4-payments queue.</summary>
public record ChargePayment(Guid OrderId, string Customer, decimal Amount);

/// <summary>Event: Payments → anyone who subscribes (here Orders).</summary>
public record PaymentCharged(Guid OrderId, string PaymentReference, decimal Amount);

/// <summary>Event: Payments → anyone who subscribes. A business outcome, not a technical error.</summary>
public record PaymentFailed(Guid OrderId, string Reason);
