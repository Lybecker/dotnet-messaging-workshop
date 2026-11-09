using Contoso.Messaging.Demo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace M3.Wolverine;

// PLUMBING – no need to show. One host per menu item in Program.cs.

/// <summary>
/// Connects to the broker from broker.json (Transport.Connect) and loads ONLY the handlers that menu item needs,
/// so the menu items in this project do not pick up each other's handlers.
/// </summary>
public static class WolverineHost
{
    public static IHost Build(Broker broker, string serviceName, Action<WolverineOptions> configure, params Type[] handlers)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.UseWolverine(opts =>
        {
            opts.ServiceName = serviceName;
            Transport.Connect(opts, broker);

            opts.Discovery.DisableConventionalDiscovery();
            foreach (var handler in handlers) opts.Discovery.IncludeType(handler);

            configure(opts);
        });
        return builder.Build();
    }

    public static Task WaitForEnter(string what)
    {
        Console.WriteLine($"{what} – press ENTER to stop");
        return Task.Run(Console.ReadLine);
    }
}

public static class Names
{
    public const string Payments = "payments-wolverine";              // the Payments process owns this queue
    public const string PaymentEvents = "payment-events-wolverine";   // Payments publishes PaymentCharged here
    public const string OrdersSubscription = "orders-wolverine";      // the Orders process's subscription
}
