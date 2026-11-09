// PLUMBING - keeps the demo's business trace visible without hiding failures.
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace M4;

public sealed class TelemetryPresentation : BaseProcessor<Activity>
{
    private const string OutboxPoll = "m4.background-outbox-poll";
    private static readonly Regex PollQuery = new(
        @"^\s*SELECT\s+TOP\s+\d+\s+\[Id\],\s*\[DestinationAddress\],\s*\[Headers\],\s*\[Body\]\s+FROM\s+(?:\[dbo\]\.|dbo\.)?\[?RebusOutbox(?:Orders|Payments)\]?\s+WITH\s*\(UPDLOCK,\s*READPAST\)\s+WHERE\s+\[Sent\]\s*=\s*0\s+ORDER\s+BY\s+\[Id\]\s*;?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static void ConfigureTracing(IServiceCollection services)
    {
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
        services.AddOpenTelemetry().WithTracing(t => t
            .AddSource("Azure.Messaging.ServiceBus.*")
            .AddSqlClientInstrumentation(options => options.EnrichWithSqlCommand = MarkOutboxPoll)
            // Register before UseAzureMonitor's exporting processors.
            .AddProcessor(new TelemetryPresentation()));
    }

    public static void MarkOutboxPoll(Activity activity, object command)
    {
        if (activity.ParentId is null
            && command is SqlCommand { CommandType: CommandType.Text } sql
            && PollQuery.IsMatch(sql.CommandText))
        {
            activity.SetCustomProperty(OutboxPoll, true);
        }
    }

    public override void OnStart(Activity activity)
    {
        if (activity.Source.Name != "Rebus.Diagnostics"
            || activity.GetTagItem("rebus.message.type") is not string messageType)
            return;

        var typeName = messageType.Split(',')[0];
        var shortName = typeName[(typeName.LastIndexOf('.') + 1)..];
        var operation = activity.OperationName[(activity.OperationName.LastIndexOf(' ') + 1)..];
        activity.DisplayName = $"{shortName} {operation}";
        activity.SetTag("messaging.system", "rebus");
        activity.SetTag("messaging.operation", operation);
    }

    public override void OnEnd(Activity activity)
    {
        if (activity.GetCustomProperty(OutboxPoll) is true
            && activity.Status != ActivityStatusCode.Error
            && activity.GetTagItem("error.type") is null
            && !activity.Events.Any(e => e.Name == "exception"))
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }
}
