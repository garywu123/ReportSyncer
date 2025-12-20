/* Schema seed for integration tests - fixture-friendly
   This file is idempotent and assumes it's executed in the current database.
*/

SET NOCOUNT ON;

/* Create dedicated schema */
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'rs_test')
    EXEC('CREATE SCHEMA rs_test');
GO

/* ----------------------------------------------------------------------------
   DROP (dependency-safe)
   ---------------------------------------------------------------------------- */

-- Drop cycle constraints first
IF OBJECT_ID('rs_test.CycleA', 'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CycleA_CycleB')
        ALTER TABLE rs_test.CycleA DROP CONSTRAINT FK_CycleA_CycleB;
END
IF OBJECT_ID('rs_test.CycleB', 'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CycleB_CycleA')
        ALTER TABLE rs_test.CycleB DROP CONSTRAINT FK_CycleB_CycleA;
END
GO

-- Drop other constraints / tables
IF OBJECT_ID('rs_test.OrderLines', 'U') IS NOT NULL DROP TABLE rs_test.OrderLines;
IF OBJECT_ID('rs_test.Orders', 'U') IS NOT NULL DROP TABLE rs_test.Orders;
IF OBJECT_ID('rs_test.Customers', 'U') IS NOT NULL DROP TABLE rs_test.Customers;

IF OBJECT_ID('rs_test.Children', 'U') IS NOT NULL DROP TABLE rs_test.Children;
IF OBJECT_ID('rs_test.Parents', 'U') IS NOT NULL DROP TABLE rs_test.Parents;

IF OBJECT_ID('rs_test.Categories', 'U') IS NOT NULL DROP TABLE rs_test.Categories;

IF OBJECT_ID('rs_test.Products', 'U') IS NOT NULL DROP TABLE rs_test.Products;

IF OBJECT_ID('rs_test.WeirdTypes', 'U') IS NOT NULL DROP TABLE rs_test.WeirdTypes;

IF OBJECT_ID('rs_test.CycleA', 'U') IS NOT NULL DROP TABLE rs_test.CycleA;
IF OBJECT_ID('rs_test.CycleB', 'U') IS NOT NULL DROP TABLE rs_test.CycleB;
GO

/* ----------------------------------------------------------------------------
   CREATE TABLES
   ---------------------------------------------------------------------------- */

CREATE TABLE rs_test.Products
(
    ProductId   INT IDENTITY(1,1) NOT NULL,
    Sku         VARCHAR(40) NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    Price       DECIMAL(12,2) NOT NULL CONSTRAINT DF_Products_Price DEFAULT (0),
    RowVer      ROWVERSION NOT NULL,
    CONSTRAINT PK_Products PRIMARY KEY (ProductId),
    CONSTRAINT UQ_Products_Sku UNIQUE (Sku)
);
GO

CREATE TABLE rs_test.Customers
(
    Id          INT IDENTITY(1,1) NOT NULL,
    ExternalKey UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Customers_ExternalKey DEFAULT (NEWID()),
    Name        VARCHAR(50) NOT NULL,
    Notes       NVARCHAR(MAX) NULL,
    CreatedUtc  DATETIME2(3) NOT NULL CONSTRAINT DF_Customers_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Customers PRIMARY KEY (Id)
);
GO

CREATE TABLE rs_test.Orders
(
    Id          INT IDENTITY(1,1) NOT NULL,
    CustomerId  INT NOT NULL,
    OrderNo     VARCHAR(32) NULL,
    OrderDate   DATE NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT (CONVERT(date, SYSUTCDATETIME())),
    OrderNoUpper AS (UPPER(OrderNo)) PERSISTED,
    CONSTRAINT PK_Orders PRIMARY KEY (Id),
    CONSTRAINT FK_Orders_Customers
        FOREIGN KEY (CustomerId) REFERENCES rs_test.Customers(Id)
        ON DELETE CASCADE
);
GO

CREATE TABLE rs_test.OrderLines
(
    Id          INT IDENTITY(1,1) NOT NULL,
    OrderId     INT NOT NULL,
    LineNum     INT NOT NULL,
    ProductSku  VARCHAR(40) NOT NULL,
    Qty         INT NOT NULL CONSTRAINT DF_OrderLines_Qty DEFAULT (1),
    CONSTRAINT PK_OrderLines PRIMARY KEY (Id),
    CONSTRAINT FK_OrderLines_Orders
        FOREIGN KEY (OrderId) REFERENCES rs_test.Orders(Id),
    CONSTRAINT FK_OrderLines_Products_BySku
        FOREIGN KEY (ProductSku) REFERENCES rs_test.Products(Sku)
);
GO

CREATE INDEX IX_OrderLines_OrderId ON rs_test.OrderLines(OrderId);
GO

CREATE TABLE rs_test.Parents
(
    ParentId    INT NOT NULL,
    RegionId    INT NOT NULL,
    Name        VARCHAR(50) NOT NULL,
    CONSTRAINT PK_Parents PRIMARY KEY (ParentId, RegionId)
);
GO

CREATE TABLE rs_test.Children
(
    ChildId     INT IDENTITY(1,1) NOT NULL,
    ParentId    INT NOT NULL,
    RegionId    INT NOT NULL,
    Notes       NVARCHAR(200) NULL,
    CONSTRAINT PK_Children PRIMARY KEY (ChildId),
    CONSTRAINT FK_Children_Parents FOREIGN KEY (ParentId, RegionId) REFERENCES rs_test.Parents(ParentId, RegionId)
);
GO

CREATE TABLE rs_test.Categories
(
    Id          INT IDENTITY(1,1) NOT NULL,
    ParentId    INT NULL,
    Name        NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_Categories PRIMARY KEY (Id),
    CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentId) REFERENCES rs_test.Categories(Id)
);
GO

CREATE TABLE rs_test.WeirdTypes
(
    Id          INT IDENTITY(1,1) NOT NULL,
    Payload     SQL_VARIANT NULL,
    JsonText    NVARCHAR(MAX) NULL,
    CONSTRAINT PK_WeirdTypes PRIMARY KEY (Id)
);
GO

CREATE TABLE rs_test.CycleA
(
    AId         INT IDENTITY(1,1) NOT NULL,
    BRefId      INT NULL,
    Name        NVARCHAR(50) NOT NULL,
    CONSTRAINT PK_CycleA PRIMARY KEY (AId)
);
GO

CREATE TABLE rs_test.CycleB
(
    BId         INT IDENTITY(1,1) NOT NULL,
    ARefId      INT NULL,
    Name        NVARCHAR(50) NOT NULL,
    CONSTRAINT PK_CycleB PRIMARY KEY (BId)
);
GO

ALTER TABLE rs_test.CycleA ADD CONSTRAINT FK_CycleA_CycleB FOREIGN KEY (BRefId) REFERENCES rs_test.CycleB(BId);
GO

ALTER TABLE rs_test.CycleB ADD CONSTRAINT FK_CycleB_CycleA FOREIGN KEY (ARefId) REFERENCES rs_test.CycleA(AId);
GO

PRINT 'Schema seed completed in current database.';
GO

SET NOCOUNT OFF;
