// EXERCISE 2 · Your first endpoint · Wolverine STARTER
// Purpose: wire two processes with Wolverine: Orders SENDS the ShipOrder command, Shipping handles it and PUBLISHES
//          OrderShipped, Orders SUBSCRIBES to it. The broker is already connected (Shared/Wolverine/Plumbing.cs).
// Show:    work through TODO 1–4 in Orders.cs and Shipping.cs. Stuck? Peek at ../Wolverine.Solution.
// Run:     two terminals, both: dotnet run [-- asb | rabbit]   (default: broker.json in the repository root)
//          pick 2 (Shipping) in one and 1 (Orders) in the other, then press ENTER in Orders.
using Contoso.Messaging.Demo;
using Exercise2;

var broker = BrokerSetting.Resolve(args);
Console.WriteLine($"Exercise 2 · Wolverine on {broker.Describe()}");

// Two processes: run each in its own terminal
var menu = new Dictionary<string, (string, Func<Task>)>
{
    ["1"] = ("Orders:   send ShipOrder, subscribe to OrderShipped", () => Orders.RunAsync(broker)),
    ["2"] = ("Shipping: handle ShipOrder, publish OrderShipped", () => Shipping.RunAsync(broker)),
};
foreach (var (k, (d, _)) in menu) Console.WriteLine($"{k}) {d}");
Console.Write("> ");
if (menu.TryGetValue(Console.ReadLine() ?? "", out var choice)) await choice.Item2();
