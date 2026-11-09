// EXERCISE 3 · Make a flaky consumer reliable · Rebus SOLUTION
// Purpose: retries with back-off for transient failures, fail fast for poison messages, a business outcome
//          for declined cards, and idempotency. The broker is already wired; every TODO is the same on both.
// Show:    compare with the other framework's solution in the debrief.
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
        .Transport(t => ExerciseTransport.Connect(t, broker, queue))      // given: the broker from broker.json
        .Routing(r => r.TypeBased().Map<ChargePayment>(queue))           // 1
        .Timeouts(t => ExerciseTransport.Timeouts(t, broker))             // given: Defer needs a store on RabbitMQ
        .Options(o =>
        {
            // 3a: 3 quick attempts, then IFailed<T> (second level), then the "error" queue
            o.RetryStrategy(errorQueueName: "error", maxDeliveryAttempts: 3, secondLevelRetriesEnabled: true);
            o.FailFastOn<UnsupportedCurrencyException>();                     // 3a: poison skips immediate retries
            o.SetMaxParallelism(5);
        }),
    onCreated: async bus =>
    {
        await bus.Subscribe<PaymentCharged>();
        await bus.Subscribe<PaymentFailed>();
    });

builder.Services.AutoRegisterHandlersFromAssemblyOf<ChargePaymentHandler>();

using var host = builder.Build();
await host.StartAsync();
var bus = host.Services.GetRequiredService<IBus>();

Console.WriteLine($"Exercise 3 · Rebus SOLUTION on {broker.Describe()} · queue {queue}");
await Menu.RunAsync(cmd => bus.Send(cmd));
await host.StopAsync();
