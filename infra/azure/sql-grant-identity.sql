-- Run ONCE against the "Messaging" database (NOT master), signed in as the Entra admin (you), e.g.:
--   sqlcmd -S <sqlServerFqdn> -d Messaging --authentication-method ActiveDirectoryDefault -i sql-grant-identity.sql
-- or in the Azure portal Query editor / Azure Data Studio / SSMS with "Microsoft Entra MFA".
--
-- Replace msgtrain-demo-id with the managedIdentityName output if you changed the prefix.

CREATE USER [msgtrain-demo-id] FROM EXTERNAL PROVIDER;

-- Demo convenience: Wolverine and Rebus create their own schemas/tables at start-up → db_owner.
-- Production: pre-create the tables via migrations and grant only db_datareader + db_datawriter (+ EXECUTE).
ALTER ROLE db_owner ADD MEMBER [msgtrain-demo-id];
GO

-- Tables for the hand-rolled Reliability Inbox/Outbox samples
IF OBJECT_ID('dbo.Inbox') IS NULL
CREATE TABLE dbo.Inbox (
    MessageId   nvarchar(200)  NOT NULL PRIMARY KEY,
    Consumer    nvarchar(100)  NOT NULL,
    ProcessedAt datetimeoffset NOT NULL DEFAULT sysdatetimeoffset());

IF OBJECT_ID('dbo.Outbox') IS NULL
CREATE TABLE dbo.Outbox (
    Id            nvarchar(200)  NOT NULL PRIMARY KEY,
    Destination   nvarchar(200)  NOT NULL,
    Type          nvarchar(200)  NOT NULL,
    CorrelationId nvarchar(200)  NOT NULL,
    Body          varbinary(max) NOT NULL,
    CreatedAt     datetimeoffset NOT NULL,
    SentAt        datetimeoffset NULL);
GO
