/* ============================================================
   Permission Profiler Test Fixture (SQL Server)
   - Database: ReportSyncer_PermTest
   - Table: rpt.TargetEvents (has IDENTITY)
   - Users:
       - rs_admin   : db_owner (all permissions)
       - rs_writer  : SELECT+INSERT, DENY DELETE, no ALTER (identity insert denied)
   ============================================================ */

IF DB_ID('ReportSyncer_PermTest') IS NOT NULL
BEGIN
    ALTER DATABASE ReportSyncer_PermTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ReportSyncer_PermTest;
END;
GO

CREATE DATABASE ReportSyncer_PermTest;
GO

USE ReportSyncer_PermTest;
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
VALUES (50, '2025-12-20', 'x');
GO

/* ---- logins/users (SQL Auth) ---- */
-- Change passwords if your policy requires
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'rs_admin')
    CREATE LOGIN rs_admin WITH PASSWORD = 'P@ssw0rd_123!Secure';

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'rs_writer')
    CREATE LOGIN rs_writer WITH PASSWORD = 'P@ssw0rd_123!Secure';
GO

USE ReportSyncer_PermTest;
GO

CREATE USER rs_admin FOR LOGIN rs_admin;
CREATE USER rs_writer FOR LOGIN rs_writer;
GO

EXEC sp_addrolemember 'db_owner', 'rs_admin';
GO

-- Writer: can read + insert, but cannot delete, cannot alter table (identity insert denied)
GRANT SELECT ON SCHEMA::rpt TO rs_writer;
GRANT INSERT ON SCHEMA::rpt TO rs_writer;
DENY DELETE ON SCHEMA::rpt TO rs_writer;
-- Do NOT grant ALTER; identity insert should fail
GO
