CREATE DATABASE IsolationDemo;
GO
USE IsolationDemo;
GO

CREATE TABLE Accounts (
    Id      INT PRIMARY KEY,
    Name    VARCHAR(50),
    Balance DECIMAL(10,2)
);

INSERT INTO Accounts VALUES (1, 'Alice', 1000.00);
INSERT INTO Accounts VALUES (2, 'Bob',    500.00);
GO
