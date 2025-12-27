IF DB_ID('ReportSyncer_EstimatorDb') IS NOT NULL
BEGIN
    ALTER DATABASE ReportSyncer_EstimatorDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ReportSyncer_EstimatorDb;
END;
GO

CREATE DATABASE ReportSyncer_EstimatorDb;
GO

USE ReportSyncer_EstimatorDb;
GO

CREATE SCHEMA app AUTHORIZATION dbo;
GO
CREATE SCHEMA rpt AUTHORIZATION dbo;
GO

CREATE TABLE app.SourceEvents
(
    EventId INT IDENTITY(1,1) NOT NULL,
    EventTime DATETIME2(0) NOT NULL,
    Payload NVARCHAR(100) NULL,
    CONSTRAINT PK_SourceEvents PRIMARY KEY (EventId)
);

CREATE TABLE rpt.TargetEvents
(
    CustomerId INT NOT NULL,
    EventId INT IDENTITY(1,1) NOT NULL,
    EventTime DATETIME2(0) NOT NULL,
    Payload NVARCHAR(100) NULL,
    CONSTRAINT PK_TargetEvents PRIMARY KEY (CustomerId, EventId)
);
GO

INSERT INTO app.SourceEvents(EventTime, Payload)
VALUES
('2025-12-20', 'a'),
('2025-12-25', 'b'),
('2025-12-26', 'c');

INSERT INTO rpt.TargetEvents(CustomerId, EventTime, Payload)
VALUES
(50, '2025-12-20', 'x'),
(50, '2025-12-21', 'y'),
(50, '2025-12-22', 'z'),
(60, '2025-12-20', 'u'),
(60, '2025-12-21', 'v');
GO
