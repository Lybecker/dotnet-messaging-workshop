using Contoso.Messaging.Demo;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

namespace M3.Wolverine;

/// <summary>
/// THE broker switch for the Wolverine demo. All Service Bus vs RabbitMQ differences live in this file;
/// the demos call these helpers and never mention a broker. Pick the broker in broker.json (repository root)
/// or per run: dotnet run -- asb | rabbit.
///
///   1. Connect        – the one line that really differs
///   2. CommandQueue   – a command has one owner: send to a queue, listen on it
///   3. Event          – publish to a topic (Service Bus) / exchange (RabbitMQ), optionally subscribe
///
/// In a real service, UseConventionalRouting() on the transport removes most of 2–3: Wolverine then names
/// queues and topics after the message types. The demos use explicit names so you can find them in the UIs.
/// </summary>
public static class Transport
{
    private static RabbitMqTransportExpression? _rabbit;

    // ── 1. Connect ────────────────────────────────────────────────────────────────────────────
    public static void Connect(WolverineOptions opts, Broker broker)
    {
        switch (broker)
        {
            case Broker.ServiceBus:
                var asb = DemoEnvironment.UseAzure
                    // Azure: managed identity / your az login – needs "Azure Service Bus Data Owner" for AutoProvision
                    ? opts.UseAzureServiceBus(DemoEnvironment.ServiceBusNamespace!, DemoEnvironment.Credential)
                    // Local: the emulator (Wolverine knows its AMQP 5672 + management 5300 ports)
                    : opts.UseAzureServiceBusEmulator();
                asb.AutoProvision().SystemQueuePrefix("training");   // dev convenience; test/prod: Bicep owns topology
                break;

            case Broker.RabbitMq:
                _rabbit = opts.UseRabbitMq(new Uri(DemoEnvironment.RabbitMqUri)).AutoProvision();
                _rabbit.UseQuorumQueues();   // replicated queues: the RabbitMQ default for durable queues
                break;
        }
    }

    // ── 2. A command queue ────────────────────────────────────────────────────────────────────
    /// <param name="listen">false = this process only sends (another process owns the queue)</param>
    public static void CommandQueue<T>(WolverineOptions opts, Broker broker, string queue, bool listen = true)
    {
        switch (broker)
        {
            case Broker.ServiceBus:
                opts.PublishMessage<T>().ToAzureServiceBusQueue(queue);
                if (listen) opts.ListenToAzureServiceBusQueue(queue);
                break;

            case Broker.RabbitMq:
                opts.PublishMessage<T>().ToRabbitQueue(queue);
                if (listen) opts.ListenToRabbitQueue(queue);
                break;
        }
    }

    // ── 3. An event ───────────────────────────────────────────────────────────────────────────
    /// <param name="subscription">null = publish only. Otherwise: our own subscription (Service Bus) / bound queue (RabbitMQ)</param>
    public static void Event<T>(WolverineOptions opts, Broker broker, string topic, string? subscription = null)
    {
        switch (broker)
        {
            case Broker.ServiceBus:
                opts.PublishMessage<T>().ToAzureServiceBusTopic(topic);
                // Own, unfiltered subscription: Wolverine doesn't set Subject/Label, so a Label filter wouldn't match
                if (subscription is not null)
                    opts.ListenToAzureServiceBusSubscription(subscription).FromTopic(topic);
                break;

            case Broker.RabbitMq:
                opts.PublishMessage<T>().ToRabbitExchange(topic);
                if (subscription is not null)
                {
                    _rabbit!.DeclareExchange(topic, ex => ex.BindQueue(subscription));
                    opts.ListenToRabbitQueue(subscription);
                }
                break;
        }
    }
}
