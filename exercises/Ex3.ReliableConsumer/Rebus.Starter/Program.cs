// EXERCISE 3 · Make a flaky consumer reliable · Rebus STARTER
// Purpose: retries with back-off for transient failures, fail fast for poison messages, a business outcome
//          for declined cards, and idempotency. The broker is already wired; every TODO is the same on both.
// Show:    work through TODO 1–4 (Program.cs, then Handlers.cs). Stuck? Peek at ../Rebus.Solution.
// Run:     dotnet run [-- asb | rabbit]   (default: broker.json in the repository root) · RabbitMQ UI http://localhost:15673
using Contoso.Messaging.Contracts;
using Contoso.Messaging.Demo;
using Exercise3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Retry.FailFast;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;

var broker = BrokerSetting.Resolve(args);
const string queue = "ex3-payments-rebus";

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<FakePaymentGateway>();

builder.Services.AddRebus(configure => configure
        .Transport(t => ExerciseTransport.Connect(t, broker, queue))      // done: the broker from broker.json
        .Routing(r => r.TypeBased()
            // TODO 1: commands are ROUTED – map ChargePayment to the queue that owns it.
            //         Same line on Service Bus and RabbitMQ.
        )
        .Timeouts(t => ExerciseTransport.Timeouts(t, broker))             // done: needed for Defer on RabbitMQ
        .Options(o =>
        {
            // TODO 3a: retries. How many immediate attempts? Where do failed messages end up?
            //          Hint: o.RetryStrategy(errorQueueName: …, maxDeliveryAttempts: …, secondLevelRetriesEnabled: …)
            //          Use o.FailFastOn<UnsupportedCurrencyException>() so poison skips immediate retries.
            o.SetMaxParallelism(5);
        }),
    onCreated: async bus =>
    {
        // Events are PUBLISHED; this console also listens to the outcome so you can see it.
        await bus.Subscribe<PaymentCharged>();
        await bus.Subscribe<PaymentFailed>();
    });

builder.Services.AutoRegisterHandlersFromAssemblyOf<ChargePaymentHandler>();

using var host = builder.Build();
await host.StartAsync();
var bus = host.Services.GetRequiredService<IBus>();

Console.WriteLine($"Exercise 3 · Rebus on {broker.Describe()} · queue {queue}");
await Menu.RunAsync(cmd => bus.Send(cmd));   // Send = command (routed), Publish = event
await host.StopAsync();
