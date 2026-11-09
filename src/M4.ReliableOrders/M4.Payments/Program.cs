// M4 · Reliable orders – the Payments process (payments-worker)
// Purpose: everything from the Reliability section in one consumer: idempotent (inbox), retries in two tiers,
//          fail fast for permanent errors, an error queue, and the outbox – all traced in Application Insights.
// Show:    the Rebus configuration below (retry tiers, fail fast, outbox), ChargePaymentHandler.cs (inbox + outbox in one
//          transaction) and ChargePaymentFailedHandler.cs (tier 2: delay or give up). Drive it from the Orders window.
// Run:     ../run-demo.sh (prints both commands), or dotnet run [-- asb | rabbit]. Needs SQL Server (docker compose).
using Contoso.Messaging.Demo;
using Contoso.Messaging.Reliability;
using M4;
using Rebus.Config;
using Rebus.Config.Outbox;
using Rebus.Retry.FailFast;
using Rebus.Retry.Simple;

var broker = BrokerSetting.Resolve(args);
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5181");
builder.AddTelemetry(roleName: "payments-worker");             // Application Insights (Telemetry.cs)
await Database.EnsureAsync();

builder.Services.AddRebus(c => c
    .Transport(t => Transport.Connect(t, broker, builder.Configuration, Transport.PaymentsQueue))
    .Timeouts(t => Transport.Timeouts(t, broker))               // delayed retries (Defer)
    .Outbox(o => o.StoreInSqlServer(Database.ConnectionString, "RebusOutboxPayments"))
    .Options(o =>
    {
        o.EnableDiagnosticSources();                            // spans + trace context in the message headers
        // Tier 1: 3 immediate attempts. Tier 2: IFailed<ChargePayment> – we decide (ChargePaymentFailedHandler.cs)
        o.RetryStrategy(errorQueueName: Transport.ErrorQueue, maxDeliveryAttempts: 3, secondLevelRetriesEnabled: true);
        o.FailFastOn<BusinessRuleViolationException>();         // permanent: no point in retrying
        o.SetNumberOfWorkers(1);
        o.SetMaxParallelism(4);
    }));

builder.Services.AddRebusHandler<ChargePaymentHandler>();
builder.Services.AddRebusHandler<ChargePaymentFailedHandler>();

var app = builder.Build();
app.MapGet("/", () => "payments-worker running");
Console.WriteLine($"payments-worker on {Transport.Describe(broker, builder.Configuration)} · queue {Transport.PaymentsQueue}");
app.Run();
