using System.Net.Http.Json;

/// <summary>
/// The demo keys. Each order is a real HTTP call to POST /orders, so in Application Insights every order
/// starts as a request and you can follow it through the broker into payments-worker and back.
/// </summary>
public class Keyboard(DemoContext demo, IConfiguration config, IHostApplicationLifetime app) : BackgroundService
{
    private static readonly HttpClient Http = new() { BaseAddress = new Uri("http://localhost:5180") };

    protected override Task ExecuteAsync(CancellationToken stop) => Task.Run(async () =>
    {
        await Task.Delay(2000, stop);   // let the host start
        ShowInstructions();
        while (!stop.IsCancellationRequested && Console.ReadLine() is { } key)
        {
            switch (key.Trim())
            {
                case "": await Order(new("alice")); break;
                case "t": await Order(new("flaky")); break;
                case "f": await Order(new("declined")); break;
                case "d": await Order(new("alice", Duplicate: true)); break;
                case "k": await Order(new("slow")); break;
                case "x": await Order(new("alice", CrashAfterCommit: true)); break;
                case "r": await Redrive.RunAsync(demo.Broker, config); break;
                case "s": foreach (var o in await OrderStatus.LatestAsync()) Console.WriteLine($"  {o.Id.ToString()[..8]}  {o.Customer,-9} {o.Status}"); break;
                case "w": await WarmUp(stop); break;
                case "q": app.StopApplication(); return;
            }
            ShowInstructions();
        }
    }, stop);

    private static void ShowInstructions() => Console.WriteLine("""

        ENTER  happy path (alice)              t  transient failure (flaky)        f  permanent failure (declined)
        d      duplicate message (same id)     k  slow payment: kill payments-worker while it works
        x      crash orders-api after commit   r  re-drive the error queue         s  order status    w  warm-up traffic    q  quit
        """);

    private static async Task Order(NewOrder order)
    {
        try
        {
            var response = await Http.PostAsJsonAsync("/orders", order);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            Console.WriteLine($"→ order {body?["orderId"]} ({order.Customer}{(order.Duplicate ? ", sent twice" : "")}): Pending");
        }
        catch (HttpRequestException) { /* 'x': the process is going down */ }
    }

    /// <summary>Two minutes of mixed traffic, so the Application Map and the charts have something to show.</summary>
    private static async Task WarmUp(CancellationToken stop)
    {
        string[] customers = ["alice", "alice", "alice", "bob", "carol", "flaky", "declined"];
        Console.WriteLine("Warm-up: 60 orders over 2 minutes…");
        for (var i = 0; i < 60 && !stop.IsCancellationRequested; i++)
        {
            await Order(new(customers[Random.Shared.Next(customers.Length)], Amount: Random.Shared.Next(10, 500)));
            await Task.Delay(2000, stop);
        }
    }
}
