-- Tables for the hand-rolled patterns in src/Reliability (Wolverine/Rebus create their own tables)
IF DB_ID('Messaging') IS NULL CREATE DATABASE Messaging;
GO
USE Messaging;
GO
CREATE TABLE dbo.Inbox (
    MessageId   nvarchar(200)  NOT NULL PRIMARY KEY,
    Consumer    nvarchar(100)  NOT NULL,
    ProcessedAt datetimeoffset NOT NULL DEFAULT sysdatetimeoffset());
CREATE TABLE dbo.Outbox (
    Id            nvarchar(200)  NOT NULL PRIMARY KEY,
    Destination   nvarchar(200)  NOT NULL,
    Type          nvarchar(200)  NOT NULL,
    CorrelationId nvarchar(200)  NOT NULL,
    Body          varbinary(max) NOT NULL,
    CreatedAt     datetimeoffset NOT NULL,
    SentAt        datetimeoffset NULL);
CREATE INDEX IX_Outbox_Unsent ON dbo.Outbox (CreatedAt) WHERE SentAt IS NULL;
