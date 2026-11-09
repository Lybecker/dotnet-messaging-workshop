using Azure.Core;
using Azure.Identity;

namespace Contoso.Messaging.Demo;

/// <summary>
/// One place that decides LOCAL (emulator / docker) vs AZURE (Entra ID + managed identity).
///
/// Azure mode is switched on by setting ASB_NAMESPACE (e.g. "msgtrain-sb-abc123.servicebus.windows.net").
/// Authentication is ALWAYS Entra ID – no connection strings, no SAS keys (the namespace has local auth disabled):
///   * On your laptop  → DefaultAzureCredential uses your `az login` / Visual Studio sign-in (you were granted RBAC roles).
///   * In Azure        → the same code uses the user-assigned managed identity when AZURE_CLIENT_ID is set
///                       (Container Apps / App Service / AKS workload identity / VM).
/// Values come from the outputs of infra/azure/main.bicep (see infra/azure/README.md).
/// </summary>
public static class DemoEnvironment
{
    public const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    /// <summary>Fully qualified Service Bus namespace, or null when running against the local emulator.</summary>
    public static string? ServiceBusNamespace => Environment.GetEnvironmentVariable("ASB_NAMESPACE");

    public static bool UseAzure => !string.IsNullOrWhiteSpace(ServiceBusNamespace);

    /// <summary>Managed identity when AZURE_CLIENT_ID is set, otherwise the developer's own Entra sign-in.</summary>
    public static TokenCredential Credential { get; } = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ManagedIdentityClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")
    });

    /// <summary>
    /// Azure SQL with Entra-only auth: "Active Directory Default" makes Microsoft.Data.SqlClient use the same
    /// DefaultAzureCredential chain (managed identity via AZURE_CLIENT_ID, else your az login).
    /// </summary>
    public static string SqlConnectionString =>
        Environment.GetEnvironmentVariable("SQL_CONNECTION")
        ?? "Server=localhost,1433;User Id=sa;Password=Training#2026;TrustServerCertificate=true;Database=Messaging";

    public static string RabbitMqUri =>
        Environment.GetEnvironmentVariable("RABBITMQ_URI") ?? "amqp://guest:guest@localhost:5673/";
}
