--Create the Database--
USE master;
GO
IF DB_ID('CoveringIndexDemo') IS NOT NULL
BEGIN
    ALTER DATABASE CoveringIndexDemo SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE CoveringIndexDemo;
END
GO
CREATE DATABASE CoveringIndexDemo;
GO
USE CoveringIndexDemo;
GO
CREATE TABLE dbo.Orders
(
    OrderID      INT           NOT NULL IDENTITY(1,1),
    CustomerID   INT           NOT NULL,
    OrderDate    DATE          NOT NULL,
    Status       NVARCHAR(20)  NOT NULL,
    TotalAmount  DECIMAL(10,2) NOT NULL,
    ShipCity     NVARCHAR(50)  NOT NULL,
    Notes        NVARCHAR(500) NULL,
    CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (OrderID)
);
GO


--Insert 50,000 rows--
USE CoveringIndexDemo;
GO
;WITH Numbers AS
(
    SELECT TOP (50000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_columns a CROSS JOIN sys.all_columns b
)
INSERT INTO dbo.Orders (CustomerID, OrderDate, Status, TotalAmount, ShipCity, Notes)
SELECT
    ABS(CHECKSUM(NEWID())) % 1000 + 1,
    DATEADD(DAY, -(n % 730), CAST('2024-01-01' AS DATE)),
    CASE (n % 4)
        WHEN 0 THEN 'Pending'
        WHEN 1 THEN 'Shipped'
        WHEN 2 THEN 'Delivered'
        ELSE        'Cancelled'
    END,
    CAST((ABS(CHECKSUM(NEWID())) % 90000 + 1000) / 100.0 AS DECIMAL(10,2)),
    CASE (n % 5)
        WHEN 0 THEN 'Mumbai'
        WHEN 1 THEN 'Delhi'
        WHEN 2 THEN 'Bangalore'
        WHEN 3 THEN 'Chennai'
        ELSE        'Hyderabad'
    END,
    REPLICATE(N'x', n % 200)
FROM Numbers;
GO
SELECT COUNT(*) AS TotalRows FROM dbo.Orders;  -- should show 50000


--Create the NON-covering index--

USE CoveringIndexDemo;
GO
CREATE INDEX IX_Orders_CustomerID
    ON dbo.Orders (CustomerID);
GO


--Run the BEFORE query--
USE CoveringIndexDemo;
GO
SET STATISTICS IO ON;
SELECT OrderID, OrderDate, Status, TotalAmount, ShipCity
FROM   dbo.Orders
WHERE  CustomerID = 42
  AND  Status = 'Shipped';
SET STATISTICS IO OFF;


--Drop old index, create the Covering index--
USE CoveringIndexDemo;
GO

DROP INDEX IX_Orders_CustomerID ON dbo.Orders;
GO

CREATE INDEX IX_Orders_CustomerID_Status_Covering
    ON dbo.Orders (CustomerID, Status)
    INCLUDE (OrderDate, TotalAmount, ShipCity);
GO


---Run the AFTER query--
USE CoveringIndexDemo;
GO
SET STATISTICS IO ON;
SELECT OrderID, OrderDate, Status, TotalAmount, ShipCity
FROM   dbo.Orders
WHERE  CustomerID = 42
  AND  Status = 'Shipped';
SET STATISTICS IO OFF;


--Side-by-side comparison--
USE CoveringIndexDemo;
GO
DROP INDEX IX_Orders_CustomerID_Status_Covering ON dbo.Orders;
GO
CREATE INDEX IX_Orders_CustomerID_Baseline ON dbo.Orders (CustomerID);
GO
SET STATISTICS IO ON;
PRINT '=== BEFORE (non-covering) ===';
SELECT OrderID, OrderDate, Status, TotalAmount, ShipCity
FROM   dbo.Orders
WHERE  CustomerID = 42 AND Status = 'Shipped';
SET STATISTICS IO OFF;
GO
DROP INDEX IX_Orders_CustomerID_Baseline ON dbo.Orders;
GO
CREATE INDEX IX_Orders_CustomerID_Status_Covering
    ON dbo.Orders (CustomerID, Status)
    INCLUDE (OrderDate, TotalAmount, ShipCity);
GO
SET STATISTICS IO ON;
PRINT '=== AFTER (covering index) ===';
SELECT OrderID, OrderDate, Status, TotalAmount, ShipCity
FROM   dbo.Orders
WHERE  CustomerID = 42 AND Status = 'Shipped';
SET STATISTICS IO OFF;
GO