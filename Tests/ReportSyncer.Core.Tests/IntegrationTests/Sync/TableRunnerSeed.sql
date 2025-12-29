/* ============================================================
   TableRunner Integration Seed (SQL Server)
   Database: ReportSyncer_TableRunnerDb
   Schemas: src (source), rpt (target)
   Tables:
     - src.SourceEvents (identity)
     - rpt.TargetEvents (identity)
============================================================ */

IF DB_ID('ReportSyncer_TableRunnerDb') IS NOT NULL
BEGIN
    ALTER DATABASE ReportSyncer_TableRunnerDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ReportSyncer_TableRunnerDb;
END;
GO

CREATE DATABASE ReportSyncer_TableRunnerDb;
GO

USE ReportSyncer_TableRunnerDb;
GO

CREATE SCHEMA src AUTHORIZATION dbo;
GO
CREATE SCHEMA rpt AUTHORIZATION dbo;
GO

CREATE TABLE src.SourceEvents
(
    CustomerId INT NOT NULL,
    EventId INT IDENTITY(1,1) NOT NULL,
    EventTime DATETIME2(0) NOT NULL,
    Payload NVARCHAR(100) NULL,
    CONSTRAINT PK_SourceEvents PRIMARY KEY (CustomerId, EventId)
);
GO

SET IDENTITY_INSERT src.SourceEvents ON;
INSERT INTO src.SourceEvents(CustomerId, EventId, EventTime, Payload)
VALUES
(1, 100, '2025-12-20', 'src-a'),
(1, 101, '2025-12-21', 'src-b'),
(2, 200, '2025-12-22', 'src-c');
SET IDENTITY_INSERT src.SourceEvents OFF;
GO

CREATE TABLE rpt.TargetEvents
(
    CustomerId INT NOT NULL,
    EventId INT IDENTITY(1,1) NOT NULL,
    EventTime DATETIME2(0) NOT NULL,
    Payload NVARCHAR(100) NULL,
    CONSTRAINT PK_TargetEvents PRIMARY KEY (CustomerId, EventId)
);
GO

-- Seed target with stale data for CustomerId=1 and keep CustomerId=2
INSERT INTO rpt.TargetEvents(CustomerId, EventTime, Payload)
VALUES
(1, '2025-11-01', 'old-1'),
(1, '2025-11-02', 'old-2'),
(2, '2025-12-20', 'keep-2');
GO
