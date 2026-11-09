using Contoso.Messaging.Reliability;
using M4;

/// <summary>The fake card gateway. The customer name picks the behaviour (see Messages.cs).</summary>
public static class PaymentGateway
{
    public static async Task ChargeAsync(ChargePayment cmd, int delayedRetries, ILogger log)
    {
        switch (cmd.Customer)
        {
            case "declined":
                throw new BusinessRuleViolationException("Card declined");              // permanent
            case "flaky" when delayedRetries == 0:
                throw new TimeoutException("Payment gateway timed out");                // transient: fine after one delay
            case "slow":
                log.LogWarning("Slow gateway: close this window NOW to simulate a crash (15 s)");
                await Task.Delay(TimeSpan.FromSeconds(15));
                break;
            default:
                await Task.Delay(Random.Shared.Next(50, 300));
                break;
        }
    }
}
