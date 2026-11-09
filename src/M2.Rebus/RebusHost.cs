using Contoso.Messaging.Demo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Config;

namespace M2.Rebus;

// PLUMBING – no need to show. One host per menu item in Program.cs.

/// <summary>
/// Connects to the broker from broker.json (Transport.Connect) and registers ONLY the handlers that menu item
/// needs (via <paramref name="services"/>), so the menu items don't pick up each other's handlers.
/// </summary>
public static class RebusHost
{
    public static IHost Build(
        Broker broker,
        string inputQueue,
        Func<RebusConfigurer, RebusConfigurer> configure,
        Action<IServiceCollection>? services = null,
        Func<global::Rebus.Bus.IBus, Task>? onCreated = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        services?.Invoke(builder.Services);

        builder.Services.AddRebus(
            configure: c => configure(c.Transport(t => Transport.Connect(t, broker, inputQueue))),
            onCreated: onCreated);

        return builder.Build();
    }

    public static Task WaitForEnter(string what)
    {
        Console.WriteLine($"{what} – press ENTER to stop");
        return Task.Run(Console.ReadLine);
    }
}

public static class Queues
{
    public const string Orders = "orders-rebus";      // the Orders process (it receives PaymentCharged here)
    public const string Payments = "payments-rebus";  // the Payments process owns this queue
}
