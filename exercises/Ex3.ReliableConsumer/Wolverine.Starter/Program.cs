// EXERCISE 3 · Make a flaky consumer reliable · Wolverine STARTER
// Purpose: retries with back-off for transient failures, fail fast for poison messages, a business outcome
//          for declined cards, and idempotency. The broker is already wired; every TODO is the same on both.
// Show:    work through TODO 1–4 (Program.cs, then Handlers.cs). Stuck? Peek at ../Wolverine.Solution.
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
    ExerciseTransport.Connect(opts, broker);                              // done: the broker from broker.json

    // TODO 1: send ChargePayment THROUGH THE BROKER and listen to it – one line, same on both brokers:
    //   ExerciseTransport.CommandQueue<ChargePayment>(opts, broker, queue);
    //   Without it, Wolverine happily runs the handler in-process – it "works", but nothing ever touches
    //   the broker. Check the RabbitMQ UI (or Service Bus Explorer): is there a queue called ex3-payments-wolverine?

    // TODO 3: error policies – one rule per KIND of failure, declared once for all handlers.
    //   GatewayTimeoutException      → retry quickly, then retry later with back-off, then error queue
    //   UnsupportedCurrencyException → straight to the error queue (no retries)
    //   Hint: opts.Policies.OnException<T>().RetryWithCooldown(…).Then.ScheduleRetry(…).Then.MoveToErrorQueue();
});

using var host = builder.Build();
await host.StartAsync();
var bus = host.Services.GetRequiredService<IMessageBus>();

Console.WriteLine($"Exercise 3 · Wolverine on {broker.Describe()} · queue {queue}");
await Menu.RunAsync(cmd => bus.SendAsync(cmd).AsTask());
await host.StopAsync();
