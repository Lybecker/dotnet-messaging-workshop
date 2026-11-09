using System.Diagnostics;
using M4;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

const string poll = "SELECT TOP 100 [Id], [DestinationAddress], [Headers], [Body] FROM [dbo].[RebusOutboxOrders] WITH (UPDLOCK, READPAST) WHERE [Sent] = 0 ORDER BY [Id]";
var exporter = new CaptureExporter();
var services = new ServiceCollection();
TelemetryPresentation.ConfigureTracing(services);
services.AddOpenTelemetry().WithTracing(t => t
    .AddSource("M4.Tests", "Rebus.Diagnostics")
    .AddProcessor(new SimpleActivityExportProcessor(exporter)));
using var container = services.BuildServiceProvider();
using var provider = container.GetRequiredService<TracerProvider>();
using var source = new ActivitySource("M4.Tests");
using var rebus = new ActivitySource("Rebus.Diagnostics");

void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    Console.WriteLine($"PASS: {message}");
}

void SqlSpan(string name, string sql, bool failed = false, bool exception = false, string? errorType = null)
{
    using var activity = source.StartActivity(name, ActivityKind.Client)
        ?? throw new InvalidOperationException("Missing SQL test activity");
    using var command = new SqlCommand(sql);
    TelemetryPresentation.MarkOutboxPoll(activity, command);
    if (failed) activity.SetStatus(ActivityStatusCode.Error);
    if (exception) activity.AddEvent(new ActivityEvent("exception"));
    if (errorType is not null) activity.SetTag("error.type", errorType);
}

SqlSpan("successful poll", poll);
Check(!exporter.Names.Contains("successful poll"), "successful background poll is not exported");
SqlSpan("payments poll", poll.Replace("RebusOutboxOrders", "RebusOutboxPayments"));
Check(!exporter.Names.Contains("payments poll"), "successful Payments background poll is not exported");
SqlSpan("failed poll", poll, failed: true);
SqlSpan("exception poll", poll, exception: true);
SqlSpan("error-type poll", poll, errorType: "SqlException");
Check(new[] { "failed poll", "exception poll", "error-type poll" }.All(exporter.Names.Contains),
    "failed polling spans and exceptions remain exported");
SqlSpan("business SQL", "INSERT INTO dbo.Orders (Id) VALUES (@id)");
SqlSpan("outbox insert", "INSERT INTO dbo.RebusOutboxOrders (Body) VALUES (@body)");
SqlSpan("other table", poll.Replace("RebusOutboxOrders", "SomeOtherTable"));
SqlSpan("unexpected query", poll + "; SELECT 1");
Check(new[] { "business SQL", "outbox insert", "other table", "unexpected query" }.All(exporter.Names.Contains),
    "business operations, outbox writes and unrecognized SQL remain exported");
using (source.StartActivity("business request", ActivityKind.Server))
{
    SqlSpan("poll within request", poll);
}
Check(exporter.Names.Contains("poll within request"), "polling within a business trace remains exported");

ActivityTraceId traceId;
ActivitySpanId parentSpanId;
using (var parent = source.StartActivity("send request")!)
{
    traceId = parent.TraceId;
    parentSpanId = parent.SpanId;
    using var send = rebus.StartActivity("M4.ChargePayment, M4.Shared send", ActivityKind.Client,
        parent.Context, [new("rebus.message.type", "M4.ChargePayment, M4.Shared")])!;
    Check(send.DisplayName == "ChargePayment send", "Rebus span uses a short message name");
    Check(send.GetTagItem("messaging.system") as string == "rebus", "logical messaging dependency is labelled Rebus");
    Check(send.TraceId == traceId && send.ParentSpanId == parentSpanId, "messaging correlation is unchanged");
}
using (var receive = rebus.StartActivity("M4.PaymentFailed, M4.Shared receive", ActivityKind.Consumer,
    default(ActivityContext), [new("rebus.message.type", "M4.PaymentFailed, M4.Shared")])!)
{
    Check(receive.DisplayName == "PaymentFailed receive", "receive spans get readable names");
}
using (var process = rebus.StartActivity("M4.ChargePayment, M4.Shared process", ActivityKind.Internal,
    default(ActivityContext), [new("rebus.message.type", "M4.ChargePayment, M4.Shared")])!)
{
    Check(process.DisplayName == "ChargePayment process", "process spans get readable names");
}

// Use an isolated content root so real demo.settings.json is never read by this check.
var temp = Path.Combine(Path.GetTempPath(), $"m4-telemetry-{Guid.NewGuid():N}");
Directory.CreateDirectory(temp);
try
{
    foreach (var roleName in new[] { "orders-api", "payments-worker" })
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = temp });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000001;IngestionEndpoint=http://localhost:1/"
        });
        builder.AddTelemetry(roleName);
        using var app = builder.Build();
        var configured = app.Services.GetRequiredService<TracerProvider>();
        var attributes = configured.GetResource().Attributes.ToDictionary();
        Check(attributes["service.name"] as string == roleName, $"{roleName}: role name is preserved");
        Check(attributes["service.instance.id"] as string == $"{Environment.MachineName}:{roleName}",
            $"{roleName}: role instance is stable across restarts and distinct per machine/process role");
    }
}
finally
{
    Directory.Delete(temp);
}

if (args.Contains("--live-sql"))
{
    await using var connection = new SqlConnection(Database.ConnectionString);
    await connection.OpenAsync();
    var start = exporter.Names.Count;
    await using (var command = new SqlCommand(poll, connection))
    await using (var reader = await command.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync()) { }
    }
    Check(exporter.Names.Count == start, "real SQL outbox polling is suppressed by the configured pipeline");
    await using (var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Orders", connection))
    {
        await command.ExecuteScalarAsync();
    }
    Check(exporter.Names.Count == start + 1, "real business SQL remains exported exactly once");
    await connection.ChangeDatabaseAsync("master");
    var failed = false;
    try
    {
        await using var command = new SqlCommand(poll, connection);
        await command.ExecuteReaderAsync();
    }
    catch (SqlException exception) when (exception.Number == 208)
    {
        failed = true;
    }
    Check(failed && exporter.Names.Count == start + 2, "real failed outbox poll remains exported");
}

Console.WriteLine("All M4 telemetry checks passed.");

sealed class CaptureExporter : BaseExporter<Activity>
{
    public List<string> Names { get; } = [];

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch) Names.Add(activity.DisplayName);
        return ExportResult.Success;
    }
}
