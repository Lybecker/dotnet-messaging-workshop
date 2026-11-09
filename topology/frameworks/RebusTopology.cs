// ─────────────────────────────────────────────────────────────────────────────
// Rebus: pin names, set defaults explicitly, provision topology ONLY in dev.
// Teaching snippet (not in the solution build). Verify against your Rebus.AzureServiceBus /
// Rebus.RabbitMq versions.
// ─────────────────────────────────────────────────────────────────────────────
using Contracts;
using Rebus.Config;
using Rebus.Topic;
using Rebus.Retry.Simple;

public static class RebusTopology
{
    // 1. Names are part of the contract. Renaming or moving a C# class must NOT
    //    change a topic name or the message type header.
    static readonly Dictionary<Type, string> Names = new()
    {
        [typeof(OrderPlaced)]    = "orders.order-placed.v1",
        [typeof(OrderCancelled)] = "orders.order-cancelled.v1",
        [typeof(PaymentFailed)]  = "payments.payment-failed.v1",
    };

    public static IServiceCollection AddBillingBus(this IServiceCollection services,
        IHostEnvironment env, string ns, Azure.Core.TokenCredential credential)
    {
        var isDev = env.IsDevelopment();

        return services.AddRebus((configure, _) => configure
            .Transport(t =>
            {
                var asb = t.UseAzureServiceBus($"Endpoint=sb://{ns}/", "billing", credential)
                    // 2. Defaults, explicit. (Applied when Rebus creates the queue = dev.
                    //    In test/prod the same values live in Bicep.)
                    .SetMessagePeekLockDuration(TimeSpan.FromMinutes(1))
                    .SetDefaultMessageTimeToLive(TimeSpan.FromDays(14))
                    .AutomaticallyRenewPeekLock()
                    .EnablePrefetching(20);

                // 3. Test and prod: the app has data-plane rights only (Sender/Receiver).
                if (!isDev)
                {
                    asb.DoNotCreateQueues()             // queues come from IaC
                       .DoNotCheckQueueConfiguration()  // no admin calls at startup
                       .DoNotConfigureTopic();          // Subscribe() no longer creates topics/subscriptions
                }
            })
            .Serialization(s => s.UseCustomMessageTypeNames()     // rbs2-msg-type header
                .AddWithCustomName<OrderPlaced>(Names[typeof(OrderPlaced)])
                .AddWithCustomName<OrderCancelled>(Names[typeof(OrderCancelled)])
                .AddWithCustomName<PaymentFailed>(Names[typeof(PaymentFailed)]))
            .Options(o =>
            {
                o.Decorate<ITopicNameConvention>(_ => new PinnedTopicNames(Names));
                o.RetryStrategy(errorQueueName: "billing.error",
                                maxDeliveryAttempts: 5,          // < maxDeliveryCount (10) in Bicep
                                secondLevelRetriesEnabled: true);
            }),
            onCreated: async bus =>
            {
                // Dev: creates topic + subscription (forwarding to the 'billing' queue).
                // Test/prod: a no-op because of DoNotConfigureTopic(); the subscription is in Bicep.
                await bus.Subscribe<OrderPlaced>();
                await bus.Subscribe<OrderCancelled>();
            });
    }

    // RabbitMQ variant of the transport block:
    //   t.UseRabbitMq(connectionString, "billing.order-placed")
    //    .ExchangeNames(directExchangeName: "contoso.direct", topicExchangeName: "contoso.topics")
    //    .InputQueueOptions(q => q.AddArgument("x-queue-type", "quorum"))
    //    .Prefetch(20)
    //    .Declarations(declareExchanges: isDev, declareInputQueue: isDev, bindInputQueue: isDev);
}

/// Unknown type = fail fast at startup instead of silently inventing a topic.
sealed class PinnedTopicNames(IReadOnlyDictionary<Type, string> names) : ITopicNameConvention
{
    public string GetTopic(Type eventType) =>
        names.TryGetValue(eventType, out var name)
            ? name
            : throw new InvalidOperationException($"No topic name pinned for {eventType.FullName}");
}
