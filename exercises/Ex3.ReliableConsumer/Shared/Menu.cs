using Contoso.Messaging.Contracts;

namespace Exercise3;

/// <summary>Console menu that sends ChargePayment commands. Same for the Rebus and the Wolverine version.</summary>
public static class Menu
{
    public static async Task RunAsync(Func<ChargePayment, Task> send)
    {
        Console.WriteLine("""

            ENTER = normal payment   d = declined card (5000 EUR)   p = poison (currency XXX)
            r     = same command sent twice (simulates a producer retry)   b = burst of 10   q = quit
            """);
        while (Console.ReadLine() is { } key && key != "q")
        {
            var cmd = new ChargePayment(Guid.NewGuid(), Guid.NewGuid(), 99.95m, "EUR");
            switch (key)
            {
                case "d": cmd = cmd with { Amount = 5000m }; break;
                case "p": cmd = cmd with { Currency = "XXX" }; break;
                case "r": await send(cmd); break;                       // sent once here, once more below
                case "b":
                    for (var i = 0; i < 9; i++) await send(cmd with { OrderId = Guid.NewGuid() });
                    break;
            }
            await send(cmd);
            Console.WriteLine($"→ sent ChargePayment {cmd.OrderId} ({cmd.Amount} {cmd.Currency})");
        }
    }
}
