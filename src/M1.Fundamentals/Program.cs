// M1 · Fundamentals – synchronous vs asynchronous communication
// Purpose: show temporal coupling. The synchronous call fails when the payment service is down; the queued
//          call succeeds now and the work is done later. No broker needed: a Channel stands in for the queue.
// Show:    run it with the service down, then flip IsUp to true below and run again.
// Run:     dotnet run
using System.Diagnostics;
using System.Threading.Channels;
using Contoso.Messaging.Contracts;

var payments = new FlakyPaymentService { IsUp = false };   // flip to true to compare

Console.WriteLine("── 1. Synchronous request/response (HTTP-style) ──");
var sw = Stopwatch.StartNew();
try
{
  // Caller blocks; availability(caller) = availability(caller) × availability(payments)
  var receipt = await payments.ChargeAsync(new ChargePayment(Guid.NewGuid(), Guid.NewGuid(), 99m, "EUR"));
  Console.WriteLine($"Order accepted, receipt {receipt} in {sw.ElapsedMilliseconds} ms");
}
catch (Exception ex)
{
  Console.WriteLine($"Order REJECTED because payments is down: {ex.Message} ({sw.ElapsedMilliseconds} ms)");
}

Console.WriteLine();
Console.WriteLine("── 2. Asynchronous messaging (queue in the middle) ──");
var queue = Channel.CreateUnbounded<ChargePayment>();      // stands in for a broker queue
sw.Restart();
await queue.Writer.WriteAsync(new ChargePayment(Guid.NewGuid(), Guid.NewGuid(), 99m, "EUR"));
Console.WriteLine($"Order accepted (202 Accepted) in {sw.ElapsedMilliseconds} ms – payment is pending");

// The consumer runs independently. It retries until payments is back (load-levelling/buffering).
_ = Task.Run(async () =>
{
  await foreach (var cmd in queue.Reader.ReadAllAsync())
  {
    for (var attempt = 1; ; attempt++)
    {
      try
      {
        var r = await payments.ChargeAsync(cmd);
        Console.WriteLine($"   [consumer] charged {cmd.OrderId} on attempt {attempt}: {r}");
        break;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"   [consumer] attempt {attempt} failed ({ex.Message}) – will retry");
        await Task.Delay(500);
      }
    }
  }
});

await Task.Delay(1200);
Console.WriteLine("   ...payments service comes back up");
payments.IsUp = true;
await Task.Delay(1000);

Console.WriteLine();
Console.WriteLine("Discussion: what did we trade? (eventual consistency, UX for 'pending', idempotency on retry,");
Console.WriteLine("            where do failures go, how does the user learn the outcome?)");

sealed class FlakyPaymentService
{
  public bool IsUp { get; set; }
  public async Task<string> ChargeAsync(ChargePayment cmd)
  {
    await Task.Delay(100);
    return IsUp ? $"PAY-{cmd.OrderId.ToString()[..8]}" : throw new HttpRequestException("503 Service Unavailable");
  }
}
