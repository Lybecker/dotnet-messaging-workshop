// M3 · Wolverine – the same two processes as M2.Rebus, with the same menu numbers
// Purpose: Orders sends a command, Payments handles it and returns an event – convention handlers, no broker code.
//          The broker is a setting (broker.json); Transport.cs is the only broker-specific code.
// Show:    menu 2 (Payments) in one terminal, menu 1 (Orders) in another, and send a few orders.
//          Compare Orders.cs and Payments.cs with the same files in M2.Rebus.
// Run:     dotnet run [-- asb | rabbit]   (default: broker.json in the repository root)
using Contoso.Messaging.Demo;
using M3.Wolverine;

var broker = BrokerSetting.Resolve(args);
Console.WriteLine($"M3 · Wolverine on {broker.Describe()}");

// Two processes: run each in its own terminal
var menu = new Dictionary<string, (string, Func<Task>)>
{
    ["1"] = ("Orders:   send ChargePayment, subscribe to PaymentCharged", () => Orders.RunAsync(broker)),
    ["2"] = ("Payments: handle ChargePayment, publish PaymentCharged", () => Payments.RunAsync(broker)),
};
foreach (var (k, (d, _)) in menu) Console.WriteLine($"{k}) {d}");
Console.Write("> ");
if (menu.TryGetValue(Console.ReadLine() ?? "", out var choice)) await choice.Item2();
