using Contoso.Messaging.Demo;
using Microsoft.Data.SqlClient;

namespace M4;

/// <summary>PLUMBING – creates the demo tables in SQL Server (docker compose) if they are missing.</summary>
public static class Database
{
    public static string ConnectionString => DemoEnvironment.SqlConnectionString;

    public static async Task EnsureAsync()
    {
        var master = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await Exec(master, "IF DB_ID('Messaging') IS NULL CREATE DATABASE Messaging;");
        await Exec(ConnectionString, """
            IF OBJECT_ID('dbo.Orders') IS NULL
                CREATE TABLE dbo.Orders (Id uniqueidentifier PRIMARY KEY, Customer nvarchar(100) NOT NULL,
                    Amount decimal(18,2) NOT NULL, Status nvarchar(20) NOT NULL, UpdatedAt datetimeoffset NOT NULL);
            IF OBJECT_ID('dbo.Payments') IS NULL
                CREATE TABLE dbo.Payments (OrderId uniqueidentifier NOT NULL, Reference nvarchar(50) NOT NULL,
                    Amount decimal(18,2) NOT NULL, ChargedAt datetimeoffset NOT NULL DEFAULT sysdatetimeoffset());
            IF OBJECT_ID('dbo.Inbox') IS NULL
                CREATE TABLE dbo.Inbox (MessageId nvarchar(200) NOT NULL PRIMARY KEY, Consumer nvarchar(100) NOT NULL,
                    ProcessedAt datetimeoffset NOT NULL DEFAULT sysdatetimeoffset());
            """);
    }

    private static async Task Exec(string connectionString, string sql)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
