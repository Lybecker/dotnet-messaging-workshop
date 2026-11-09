using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Rebus.OpenTelemetry.Configuration;

namespace M4;

public static class Telemetry
{
    /// <summary>
    /// Everything one process needs to show up in Application Insights. The connection string comes from
    /// demo.settings.json (written by setup.sh) or APPLICATIONINSIGHTS_CONNECTION_STRING. Without it the
    /// process still runs, with console logging only.
    /// </summary>
    public static void AddTelemetry(this WebApplicationBuilder builder, string roleName)
    {
        builder.Configuration.AddJsonFile(
            Path.Combine(builder.Environment.ContentRootPath, "..", "demo.settings.json"), optional: true);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.IncludeScopes = true; o.TimestampFormat = "HH:mm:ss "; });

        var connectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("No Application Insights connection string (run ./setup.sh): console logging only.");
            return;
        }

        TelemetryPresentation.ConfigureTracing(builder.Services);

        // ── The three lines ──────────────────────────────────────────────────────────────────────────
        builder.Services.AddOpenTelemetry()
            .UseAzureMonitor(o => o.ConnectionString = connectionString)       // logs, traces, metrics, Live Metrics
            .ConfigureResource(r => r.AddService(serviceName: roleName))       // cloud_RoleName = node on the Application Map
            .WithTracing(t => t.AddRebusInstrumentation());                    // Rebus send / publish / handle spans

        builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddAttributes(
            [new KeyValuePair<string, object>("service.instance.id", $"{Environment.MachineName}:{roleName}")]));

        // Logging scopes (OrderId, MessageId, CorrelationId, Attempt) become customDimensions on every log line
        builder.Services.Configure<OpenTelemetryLoggerOptions>(o => { o.IncludeScopes = true; o.IncludeFormattedMessage = true; });
    }
}
