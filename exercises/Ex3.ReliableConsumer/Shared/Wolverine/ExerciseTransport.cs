using Contoso.Messaging.Demo;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.RabbitMQ;

namespace Exercise3;

/// <summary>
/// Already done for you: the broker comes from broker.json (repository root) or `dotnet run -- asb | rabbit`.
/// This is the ONLY broker-specific code in the Wolverine exercise – error policies and handlers are the same on both.
/// </summary>
public static class ExerciseTransport
{
    public static void Connect(WolverineOptions opts, Broker broker)
    {
        switch (broker)
        {
            case Broker.ServiceBus: opts.UseAzureServiceBusEmulator().AutoProvision(); break;
            case Broker.RabbitMq:   opts.UseRabbitMq(new Uri(DemoEnvironment.RabbitMqUri)).AutoProvision(); break;
        }
    }

    /// <summary>Send T through the broker to <paramref name="queue"/> and listen on it.</summary>
    public static void CommandQueue<T>(WolverineOptions opts, Broker broker, string queue)
    {
        switch (broker)
        {
            case Broker.ServiceBus:
                opts.PublishMessage<T>().ToAzureServiceBusQueue(queue);
                opts.ListenToAzureServiceBusQueue(queue);
                break;
            case Broker.RabbitMq:
                opts.PublishMessage<T>().ToRabbitQueue(queue);
                opts.ListenToRabbitQueue(queue);
                break;
        }
    }
}
