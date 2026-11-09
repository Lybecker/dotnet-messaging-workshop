// M2 · Rebus – two processes talking through the broker
// Purpose: Orders sends a command, Payments handles it and publishes an event – with no broker code in either.
//          The broker is a setting (broker.json); Transport.cs is the only broker-specific code.
// Show:    menu 2 (Payments) in one terminal, menu 1 (Orders) in another, and send a few orders.
//          Then switch broker.json between rabbit and asb, restart both and send again: same code, other broker.
// Run:     dotnet run [-- asb | rabbit]   (default: broker.json in the repository root)
using Contoso.Messaging.Demo;
using M2.Rebus;

var broker = BrokerSetting.Resolve(args);
Console.WriteLine($"M2 · Rebus on {broker.Describe()}");

// Two processes: run each in its own terminal
var menu = new Dictionary<string, (string, Func<Task>)>
{
    ["1"] = ("Orders:   send ChargePayment, subscribe to PaymentCharged", () => Orders.RunAsync(broker)),
    ["2"] = ("Payments: handle ChargePayment, publish PaymentCharged", () => Payments.RunAsync(broker)),
};
foreach (var (k, (d, _)) in menu) Console.WriteLine($"{k}) {d}");
Console.Write("> ");
if (menu.TryGetValue(Console.ReadLine() ?? "", out var choice)) await choice.Item2();
