// ─────────────────────────────────────────────────────────────────────────────
// Wolverine: pin names, set defaults explicitly, provision topology ONLY in dev.
// Teaching snippet (not in the solution build). Verify against your WolverineFx versions.
// ─────────────────────────────────────────────────────────────────────────────
using Contracts;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.ErrorHandling;

// 1. Pin the message type name (default = the .NET full type name).
//    In real code: put the attribute on the contract record itself.
[MessageIdentity("orders.order-placed.v1")]
public sealed record OrderPlacedV1(Guid OrderId, decimal Total);

public static class WolverineTopology
{
    public static void ConfigureBilling(this WolverineOptions opts, IHostEnvironment env,
        string ns, Azure.Core.TokenCredential credential)
    {
        var asb = opts.UseAzureServiceBus(ns, credential);

        // 2. Only dev may create topology. Test and prod: IaC + data-plane rights.
        if (env.IsDevelopment())
            asb.AutoProvision();

        // 3. Topic and subscription names are literal strings, never derived from a class name.
        opts.PublishMessage<OrderPlacedV1>()
            .ToAzureServiceBusTopic("orders.order-placed.v1");

        opts.ListenToAzureServiceBusSubscription("billing", sub =>
            {
                // 4. Explicit defaults (used when Wolverine creates it = dev;
                //    the same values live in Bicep for test/prod).
                sub.MaxDeliveryCount = 10;
                sub.LockDuration = TimeSpan.FromMinutes(1);
                sub.DefaultMessageTimeToLive = TimeSpan.FromDays(14);
                sub.DeadLetteringOnMessageExpiration = true;
            })
            .FromTopic("orders.order-placed.v1");

        // 5. Retries handled by Wolverine stay below MaxDeliveryCount.
        opts.Policies.OnException<TimeoutException>()
            .RetryWithCooldown(50.Milliseconds(), 250.Milliseconds(), 1.Seconds());

        // Note: Wolverine's request/reply "system" queues are also auto-created.
        // Outside dev, create them in IaC or disable them (see the Wolverine docs on system queues),
        // and see "externally owned" queues/exchanges for brokers you are not allowed to configure.
    }

    // RabbitMQ variant:
    //   var rabbit = opts.UseRabbitMq(new Uri(connectionString));
    //   if (env.IsDevelopment()) rabbit.AutoProvision();
    //   opts.PublishMessage<OrderPlacedV1>().ToRabbitExchange("orders.order-placed.v1");
    //   opts.ListenToRabbitQueue("billing.order-placed", q => q.QueueType = QueueType.quorum);
}
