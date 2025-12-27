/* ============================================================
   Integration Test Fixture for ReportSyncer Query Builder
   - Source: Application DB (no CustomerId)
   - Target: Reporting DB (CustomerId required)
   ============================================================ */

-- Cleanup (careful)
IF DB_ID('ReportSyncer_SourceApp') IS NOT NULL
BEGIN
    ALTER DATABASE ReportSyncer_SourceApp SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ReportSyncer_SourceApp;
END;

IF DB_ID('ReportSyncer_TargetRpt') IS NOT NULL
BEGIN
    ALTER DATABASE ReportSyncer_TargetRpt SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ReportSyncer_TargetRpt;
END;
GO

CREATE DATABASE ReportSyncer_SourceApp;
CREATE DATABASE ReportSyncer_TargetRpt;
GO

/* -----------------------------
   Source (Application)
   ----------------------------- */
USE ReportSyncer_SourceApp;
GO

CREATE SCHEMA app AUTHORIZATION dbo;
GO

CREATE TABLE app.Products
(
    ProductId INT IDENTITY(1,1) NOT NULL,
    Name NVARCHAR(80) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_SourceProducts_IsActive DEFAULT(1),
    CONSTRAINT PK_SourceProducts PRIMARY KEY (ProductId)
);

CREATE TABLE app.Orders
(
    OrderId BIGINT IDENTITY(1000,1) NOT NULL,
    OrderDate DATETIME2(0) NOT NULL,
    ProductId INT NOT NULL,
    Qty INT NOT NULL,
    CONSTRAINT PK_SourceOrders PRIMARY KEY (OrderId),
    CONSTRAINT FK_SourceOrders_Product FOREIGN KEY (ProductId) REFERENCES app.Products(ProductId)
);

CREATE TABLE app.OrderLines
(
    OrderLineId INT IDENTITY(1,1) NOT NULL,
    OrderId BIGINT NOT NULL,
    LineNum INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CONSTRAINT PK_SourceOrderLines PRIMARY KEY (OrderLineId),
    CONSTRAINT UQ_SourceOrderLines_Order_Line UNIQUE (OrderId, LineNum),
    CONSTRAINT FK_SourceOrderLines_Order FOREIGN KEY (OrderId) REFERENCES app.Orders(OrderId)
);

CREATE TABLE app.HistoryEvents
(
    EventId INT IDENTITY(1,1) NOT NULL,
    EventTime DATETIME2(0) NOT NULL,
    EventType NVARCHAR(40) NOT NULL,
    Payload NVARCHAR(200) NULL,
    CONSTRAINT PK_SourceHistoryEvents PRIMARY KEY (EventId)
);

INSERT INTO app.Products(Name) VALUES (N'Forklift'), (N'Charger'), (N'Sensor');
INSERT INTO app.Orders(OrderDate, ProductId, Qty)
VALUES
('2025-12-20', 1, 10),
('2025-12-25', 2, 5),
('2025-12-26', 3, 2);

INSERT INTO app.OrderLines(OrderId, LineNum, Amount)
VALUES
(1000, 1, 100.00),
(1000, 2, 50.00),
(1001, 1, 25.00);

INSERT INTO app.HistoryEvents(EventTime, EventType, Payload)
VALUES
('2025-12-20', N'BOOT', N'a'),
('2025-12-25', N'ALERT', N'b'),
('2025-12-26', N'ALERT', N'c');
GO


/* -----------------------------
   Target (Reporting)
   - Requires CustomerId
   - Identity columns preserved via IDENTITY_INSERT (later tasks)
   - FK uses (CustomerId, ParentId) style
   ----------------------------- */
USE ReportSyncer_TargetRpt;
GO

CREATE SCHEMA rpt AUTHORIZATION dbo;
GO

CREATE TABLE rpt.DimProduct
(
    CustomerId INT NOT NULL,
    ProductId INT IDENTITY(1,1) NOT NULL,
    Name NVARCHAR(80) NOT NULL,
    IsActive BIT NOT NULL,
    CONSTRAINT PK_DimProduct PRIMARY KEY (CustomerId, ProductId)
);

CREATE TABLE rpt.FactOrder
(
    CustomerId INT NOT NULL,
    OrderId BIGINT IDENTITY(1000,1) NOT NULL,
    OrderDate DATETIME2(0) NOT NULL,
    ProductId INT NOT NULL,
    Qty INT NOT NULL,
    CONSTRAINT PK_FactOrder PRIMARY KEY (CustomerId, OrderId),
    CONSTRAINT FK_FactOrder_DimProduct
        FOREIGN KEY (CustomerId, ProductId)
        REFERENCES rpt.DimProduct(CustomerId, ProductId)
);

CREATE TABLE rpt.FactOrderLine
(
    CustomerId INT NOT NULL,
    OrderLineId INT IDENTITY(1,1) NOT NULL,
    OrderId BIGINT NOT NULL,
    LineNum INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CONSTRAINT PK_FactOrderLine PRIMARY KEY (CustomerId, OrderLineId),
    CONSTRAINT UQ_FactOrderLine_Order_Line UNIQUE (CustomerId, OrderId, LineNum),
    CONSTRAINT FK_FactOrderLine_FactOrder
        FOREIGN KEY (CustomerId, OrderId)
        REFERENCES rpt.FactOrder(CustomerId, OrderId)
);

CREATE TABLE rpt.HistoryEvents
(
    CustomerId INT NOT NULL,
    EventId INT IDENTITY(1,1) NOT NULL,
    EventTime DATETIME2(0) NOT NULL,
    EventType NVARCHAR(40) NOT NULL,
    Payload NVARCHAR(200) NULL,
    CONSTRAINT PK_RptHistoryEvents PRIMARY KEY (CustomerId, EventId)
);

-- Seed some existing target data for two customers
SET IDENTITY_INSERT rpt.DimProduct ON;
INSERT INTO rpt.DimProduct(CustomerId, ProductId, Name, IsActive)
VALUES
(50, 1, N'Forklift_OLD', 1),
(50, 2, N'Charger_OLD', 1),
(60, 1, N'Forklift_OTHER', 1);
SET IDENTITY_INSERT rpt.DimProduct OFF;

SET IDENTITY_INSERT rpt.FactOrder ON;
INSERT INTO rpt.FactOrder(CustomerId, OrderId, OrderDate, ProductId, Qty)
VALUES
(50, 1000, '2025-12-20', 1, 999),
(60, 1000, '2025-12-20', 1, 111);
SET IDENTITY_INSERT rpt.FactOrder OFF;

SET IDENTITY_INSERT rpt.FactOrderLine ON;
INSERT INTO rpt.FactOrderLine(CustomerId, OrderLineId, OrderId, LineNum, Amount)
VALUES
(50, 1, 1000, 1, 999.00),
(60, 1, 1000, 1, 111.00);
SET IDENTITY_INSERT rpt.FactOrderLine OFF;

SET IDENTITY_INSERT rpt.HistoryEvents ON;
INSERT INTO rpt.HistoryEvents(CustomerId, EventId, EventTime, EventType, Payload)
VALUES
(50, 1, '2025-12-20', N'ALERT', N'old'),
(60, 1, '2025-12-20', N'ALERT', N'other');
SET IDENTITY_INSERT rpt.HistoryEvents OFF;
GO
