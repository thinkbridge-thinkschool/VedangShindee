/*
This query text was retrieved from showplan XML, and may be truncated.
*/


USE IndexDemo;
GO

-- Drop all indexes to start from scratch
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='CIX_Sales_SaleID'        AND object_id=OBJECT_ID('dbo.Sales')) DROP INDEX CIX_Sales_SaleID        ON dbo.Sales;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sales_SaleDate'       AND object_id=OBJECT_ID('dbo.Sales')) DROP INDEX IX_Sales_SaleDate       ON dbo.Sales;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sales_Region_Category' AND object_id=OBJECT_ID('dbo.Sales')) DROP INDEX IX_Sales_Region_Category ON dbo.Sales;
GO

-- ============================================================
-- QUERY 1: Point lookup by SaleID
-- ============================================================
CHECKPOINT; DBCC DROPCLEANBUFFERS; DBCC FREEPROCCACHE;
PRINT '=== Q1 BEFORE: Heap Scan (no index) ===';
SET STATISTICS IO ON;
SELECT SaleID, CustomerID, Amount FROM dbo.Sales WHERE SaleID = 42000;
SET STATISTICS IO OFF;

-- Create Clustered Index
CREATE CLUSTERED INDEX CIX_Sales_SaleID ON dbo.Sales (SaleID ASC);
GO

CHECKPOINT; DBCC DROPCLEANBUFFERS; DBCC FREEPROCCACHE;
PRINT '=== Q1 AFTER: Clustered Index Seek ===';
SET STATISTICS IO ON;
SELECT SaleID, CustomerID, Amount FROM dbo.Sales WHERE SaleID = 42000;
SET STATISTICS IO OFF;

-- ============================================================
-- QUERY 2: Filter by SaleDate
-- ============================================================
CHECKPOINT; DBCC DROPCLEANBUFFERS; DBCC FREEPROCCACHE;
PRINT '=== Q2 BEFORE: Clustered Index Scan (no NCX) ===';
SET STATISTICS IO ON;
SELECT SaleID, CustomerID, SaleDate, Amount FROM dbo.Sales WHERE SaleDate = '2024-06-15';
SET STATISTICS IO OFF;

-- Create Non-Clustered Index 1
CREATE NONCLUSTERED INDEX IX_Sales_SaleDate
    ON dbo.Sales (SaleDate ASC)
    INCLUDE (CustomerID, Amount);
GO

CHECKPOINT; DBCC DROPCLEANBUFFERS; DBCC FREEPROCCACHE;
PRINT '=== Q2 AFTER: Non-Clustered Index Seek (covering) ===';
SET STATISTICS IO ON;
SELECT SaleID, CustomerID, SaleDate, Amount FROM dbo.Sales WHERE SaleDate = '2024-06-15';
SET STATISTICS IO OFF;

-- ============================================================
-- QUERY 3: Filter by Region + ProductCategory
-- ============================================================
CHECKPOINT; DBCC DROPCLEANBUFFERS; DBCC FREEPROCCACHE;
PRINT '=== Q3 BEFORE: Clustered Index Scan (no NCX) ===';
SET STATISTICS IO ON;
SELECT Region, ProductCategory, COUNT(*) AS Orders, SUM(Amount) AS Revenue
FROM dbo.Sales
WHERE Region = 'North' AND ProductCategory = 'Electronics'
GROUP BY Region, ProductCategory;
SET STATISTICS IO OFF;

-- Create Non-Clustered Index 2
CREATE NONCLUSTERED INDEX IX_Sales_Region_Category
    ON dbo.Sales (Region ASC, ProductCategory ASC)
    INCLUDE (Amount);
GO

CHECKPOINT; DBCC DROPCLEANBUFFERS; DBCC FREEPROCCACHE;
PRINT '=== Q3 AFTER: Non-Clustered Index Seek (covering) ===';
SET STATISTICS IO ON;
SELECT Region, ProductCategory, COUNT(*) AS Orders, SUM(Amount) AS Revenue
FROM dbo.Sales
WHERE Region = 'North' AND ProductCategory = 'Electronics'
GROUP BY Region, ProductCategory;
SET STATISTICS IO OFF;

-- ============================================================
-- WRITE COST: See execution plan tab for 3 index nodes
-- ============================================================
PRINT '=== WRITE COST: INSERT touches all 3 indexes ===';
INSERT INTO dbo.Sales VALUES (100009, 999, 10, '2025-05-26', 'Books', 'East', 49.99);
GO
