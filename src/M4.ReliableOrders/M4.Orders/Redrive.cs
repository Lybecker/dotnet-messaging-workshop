using Contoso.Messaging.Demo;
using M4;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Messages;
using Rebus.Retry.Simple;
using Rebus.Routing.TransportMessages;

/// <summary>
/// Every error queue needs an owner and a runbook. This is the runbook as code: a small bus reads m4-error and
///   transient failures (a timeout that ran out of delayed retries) → back to the queue they came from
///   anything else (card declined: a business decision)             → m4-error-parked, for a human
/// The error details travel with the message (rbs2-error-details, rbs2-source-queue).
/// </summary>
public static class Redrive
{
    public static async Task RunAsync(Broker broker, IConfiguration config)
    {
        using var activator = new BuiltinHandlerActivator();
        using var bus = Configure.With(activator)
            .Logging(l => l.None())
            .Transport(t => Transport.Connect(t, broker, config, Transport.ErrorQueue))
            .Routing(r => r.AddTransportMessageForwarder(message =>
            {
                var source = message.Headers.GetValueOrDefault(Headers.SourceQueue) ?? Transport.PaymentsQueue;
                var details = message.Headers.GetValueOrDefault(Headers.ErrorDetails) ?? "";
                var transient = details.Contains(nameof(TimeoutException));
                Console.WriteLine($"  {message.Headers.GetValueOrDefault(Headers.MessageId)?[..8]}: {details.Split('\n')[0]}");
                Console.WriteLine(transient ? $"    → back to {source}" : $"    → {Transport.ParkedQueue} (needs a human)");
                return Task.FromResult(ForwardAction.ForwardTo(transient ? source : Transport.ParkedQueue));
            }))
            .Options(o => o.RetryStrategy(errorQueueName: Transport.ParkedQueue))   // never loop back into m4-error
            .Start();

        Console.WriteLine($"Re-driving {Transport.ErrorQueue} for 10 seconds…");
        await Task.Delay(TimeSpan.FromSeconds(10));
    }
}
