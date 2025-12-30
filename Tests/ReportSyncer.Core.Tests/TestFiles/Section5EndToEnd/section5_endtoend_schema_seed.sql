-- Integration schema and seed for Section 5 end-to-end tests.
-- Resets source (rs_src) and target (rs_tgt) schemas, builds core tables,
-- and seeds data used across IT1-IT8 scenarios.

SET NOCOUNT ON;

-- Ensure schemas exist
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'rs_src') EXEC('CREATE SCHEMA rs_src');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'rs_tgt') EXEC('CREATE SCHEMA rs_tgt');
GO

-- Drop and recreate tables (source + target)
IF OBJECT_ID('rs_src.Child', 'U') IS NOT NULL DROP TABLE rs_src.Child;
IF OBJECT_ID('rs_src.Parent', 'U') IS NOT NULL DROP TABLE rs_src.Parent;
IF OBJECT_ID('rs_src.IdentityDemo', 'U') IS NOT NULL DROP TABLE rs_src.IdentityDemo;

IF OBJECT_ID('rs_tgt.Child', 'U') IS NOT NULL DROP TABLE rs_tgt.Child;
IF OBJECT_ID('rs_tgt.Parent', 'U') IS NOT NULL DROP TABLE rs_tgt.Parent;
IF OBJECT_ID('rs_tgt.IdentityDemo', 'U') IS NOT NULL DROP TABLE rs_tgt.IdentityDemo;
IF OBJECT_ID('rs_tgt.Unrelated', 'U') IS NOT NULL DROP TABLE rs_tgt.Unrelated;
GO

CREATE TABLE rs_src.Parent (
    ParentId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    BizKey NVARCHAR(40) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    CreatedOn DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE rs_src.Child (
    ChildId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ParentId INT NOT NULL,
    LineCode NVARCHAR(40) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CreatedOn DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_rs_src_Child_Parent FOREIGN KEY (ParentId) REFERENCES rs_src.Parent(ParentId)
);
GO

CREATE TABLE rs_src.IdentityDemo (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Code NVARCHAR(40) NOT NULL
);
GO

CREATE TABLE rs_tgt.Parent (
    ParentId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    BizKey NVARCHAR(40) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    CreatedOn DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE rs_tgt.Child (
    ChildId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ParentId INT NOT NULL,
    LineCode NVARCHAR(40) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CreatedOn DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_rs_tgt_Child_Parent FOREIGN KEY (ParentId) REFERENCES rs_tgt.Parent(ParentId)
);
GO

CREATE TABLE rs_tgt.IdentityDemo (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Code NVARCHAR(40) NOT NULL
);
GO

CREATE TABLE rs_tgt.Unrelated (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Note NVARCHAR(100) NOT NULL
);
GO

-- Seed TARGET with baseline "old" data
SET IDENTITY_INSERT rs_tgt.Parent ON;
INSERT INTO rs_tgt.Parent (ParentId, BizKey, Name, CreatedOn)
VALUES (1, 'P-1', 'Old Parent 1', '2024-01-01'),
       (2, 'P-2', 'Old Parent 2', '2024-01-02'),
       (200, 'P-BULK', 'Bulk Parent', '2024-02-01');
SET IDENTITY_INSERT rs_tgt.Parent OFF;

INSERT INTO rs_tgt.Child (ParentId, LineCode, Amount, CreatedOn)
VALUES
(1, 'OLD-L1', 10.00, '2024-01-10'),
(1, 'OLD-L2', 20.00, '2024-01-11'),
(2, 'OLD-L3', 30.00, '2024-01-12');

-- Bulk rows for large-delete scenarios (target only)
;WITH nums AS (
    SELECT TOP (5000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.objects AS so
)
INSERT INTO rs_tgt.Child (ParentId, LineCode, Amount, CreatedOn)
SELECT 200,
       CONCAT('BULK-', n),
       CAST((n % 100) + 0.25 AS DECIMAL(18,2)),
       DATEADD(day, n % 30, '2024-02-01')
FROM nums;

-- Target identity demo baseline
SET IDENTITY_INSERT rs_tgt.IdentityDemo ON;
INSERT INTO rs_tgt.IdentityDemo (Id, Code)
VALUES (1, 'OLD-1'), (2, 'OLD-2');
SET IDENTITY_INSERT rs_tgt.IdentityDemo OFF;

-- Target unrelated table so tests can assert untouched
INSERT INTO rs_tgt.Unrelated (Note)
VALUES ('KEEP-ME');

-- Seed SOURCE with "new" data
SET IDENTITY_INSERT rs_src.Parent ON;
INSERT INTO rs_src.Parent (ParentId, BizKey, Name, CreatedOn)
VALUES (1, 'P-1', 'New Parent 1', '2024-03-01'),
       (2, 'P-2', 'New Parent 2', '2024-03-02'),
       (3, 'P-3', 'Extra Parent 3', '2024-03-03');
SET IDENTITY_INSERT rs_src.Parent OFF;

SET IDENTITY_INSERT rs_src.Child ON;
INSERT INTO rs_src.Child (ChildId, ParentId, LineCode, Amount, CreatedOn)
VALUES
(1, 1, 'NEW-L1', 111.00, '2024-03-10'),
(2, 1, 'NEW-L2', 222.00, '2024-03-11'),
(3, 2, 'NEW-L3', 333.00, '2024-03-12'),
(4, 3, 'NEW-L4', 444.00, '2024-03-13');
SET IDENTITY_INSERT rs_src.Child OFF;

SET IDENTITY_INSERT rs_src.IdentityDemo ON;
INSERT INTO rs_src.IdentityDemo (Id, Code)
VALUES (1000, 'SRC-1000'), (1001, 'SRC-1001');
SET IDENTITY_INSERT rs_src.IdentityDemo OFF;
