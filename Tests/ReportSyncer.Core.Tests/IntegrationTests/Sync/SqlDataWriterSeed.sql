/* ============================================================
   SqlDataWriter Integration Seed (SQL Server)
   Database: ReportSyncer_DataWriterDb
   Table: rpt.TargetEvents (IDENTITY)
============================================================ */

IF DB_ID('ReportSyncer_DataWriterDb') IS NOT NULL
BEGIN
    ALTER DATABASE ReportSyncer_DataWriterDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ReportSyncer_DataWriterDb;
END;
GO

CREATE DATABASE ReportSyncer_DataWriterDb;
GO

USE ReportSyncer_DataWriterDb;
GO

CREATE SCHEMA rpt AUTHORIZATION dbo;
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

INSERT INTO rpt.TargetEvents(CustomerId, EventTime, Payload)
VALUES
(1, '2025-12-20', 'a'),
(1, '2025-12-21', 'b'),
(1, '2025-12-22', 'c'),
(2, '2025-12-20', 'd'),
(2, '2025-12-21', 'e');
GO
