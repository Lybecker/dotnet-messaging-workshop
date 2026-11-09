// M4 · Reliable orders – the Orders process (orders-api). Start ../run-demo.sh, then drive the demo from this window.
// Purpose: one order flow that survives what the Reliability section promised: a crash between the database and the
//          broker (outbox), duplicates (inbox), transient and permanent failures (retry tiers, fail fast, error queue),
//          and a consumer crash (at-least-once). The same run feeds Application Insights for the observability slides.
// Show:    the Rebus configuration below, PlaceOrder.cs (order row + command in one transaction), Keyboard.cs (the scenarios).
//          Keys: ENTER happy · t transient · f permanent · d duplicate · k kill Payments · x crash Orders · r re-drive · s status
// Run:     ../run-demo.sh (prints both commands), or dotnet run [-- asb | rabbit]. Needs SQL Server (docker compose).
using Contoso.Messaging.Demo;
using M4;
using Rebus.Config;
using Rebus.Config.Outbox;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;

var broker = BrokerSetting.Resolve(args);
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5180");
builder.AddTelemetry(roleName: "orders-api");                  // Application Insights (Telemetry.cs)
await Database.EnsureAsync();

builder.Services.AddRebus(
    c => c
        .Transport(t => Transport.Connect(t, broker, builder.Configuration, Transport.OrdersQueue))
        .Routing(r => r.TypeBased().Map<ChargePayment>(Transport.PaymentsQueue))        // a command has ONE owner
        .Outbox(o => o.StoreInSqlServer(Database.ConnectionString, "RebusOutboxOrders"))  // order row + command: one commit
        .Options(o =>
        {
            o.EnableDiagnosticSources();                                                // spans + trace context in headers
            o.RetryStrategy(errorQueueName: Transport.ErrorQueue, maxDeliveryAttempts: 3);
        }),
    onCreated: async bus =>
    {
        await bus.Subscribe<PaymentCharged>();                  // events: anyone may subscribe (DEV: Rebus creates the subscription)
        await bus.Subscribe<PaymentFailed>();
    });

builder.Services.AddRebusHandler<PaymentChargedHandler>();      // Pending → Paid
builder.Services.AddRebusHandler<PaymentFailedHandler>();       // Pending → Failed
builder.Services.AddSingleton(new DemoContext(broker));
builder.Services.AddHostedService<Keyboard>();                  // the demo keys

var app = builder.Build();
app.MapPost("/orders", PlaceOrder.HandleAsync);                 // every order starts as an HTTP request: the root of its trace
app.MapGet("/orders", OrderStatus.ListAsync);
Console.WriteLine($"orders-api on {Transport.Describe(broker, builder.Configuration)} · queue {Transport.OrdersQueue}");
app.Run();

public record DemoContext(Broker Broker);
