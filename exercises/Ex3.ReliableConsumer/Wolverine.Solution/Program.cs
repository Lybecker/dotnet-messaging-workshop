// EXERCISE 3 · Make a flaky consumer reliable · Wolverine SOLUTION
// Purpose: retries with back-off for transient failures, fail fast for poison messages, a business outcome
//          for declined cards, and idempotency. The broker is already wired; every TODO is the same on both.
// Show:    compare with the other framework's solution in the debrief.
// Run:     dotnet run [-- asb | rabbit]   (default: broker.json in the repository root) · RabbitMQ UI http://localhost:15673
using Contoso.Messaging.Contracts;
using Contoso.Messaging.Demo;
using Exercise3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;

var broker = BrokerSetting.Resolve(args);
const string queue = "ex3-payments-wolverine";

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<FakePaymentGateway>();

builder.UseWolverine(opts =>
{
    opts.ServiceName = "ex3-payments";

    ExerciseTransport.Connect(opts, broker);                              // given: the broker from broker.json
    ExerciseTransport.CommandQueue<ChargePayment>(opts, broker, queue);    // 1: the command goes through the broker
    // PaymentCharged / PaymentFailed have a local handler → routed in-process (fine for the exercise)

    // 3: one policy per kind of failure
    opts.Policies.OnException<UnsupportedCurrencyException>().MoveToErrorQueue();          // poison: no retries
    opts.Policies.OnException<GatewayTimeoutException>()
        .RetryWithCooldown(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500))  // tier 1: in-process
        .Then.ScheduleRetry(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)) // tier 2: later
        .Then.MoveToErrorQueue();                                                           // tier 3: error queue
});

using var host = builder.Build();
await host.StartAsync();
var bus = host.Services.GetRequiredService<IMessageBus>();

Console.WriteLine($"Exercise 3 · Wolverine SOLUTION on {broker.Describe()} · queue {queue}");
await Menu.RunAsync(cmd => bus.SendAsync(cmd).AsTask());
await host.StopAsync();
