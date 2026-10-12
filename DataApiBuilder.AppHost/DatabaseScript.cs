namespace DataApiBuilder.AppHost;

internal static class DatabaseScript
{
    public static string CreateDatabaseScript(string db) => $"""
    CREATE DATABASE [{db}];
    GO

    CREATE TABLE dbo.Customers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL
    );
    GO

    CREATE TABLE dbo.SalesReps (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(150) NOT NULL,
        Region NVARCHAR(100) NOT NULL
    );
    GO

    CREATE TABLE dbo.Invoices (
        InvoiceId INT IDENTITY(1,1) PRIMARY KEY,
        CustomerId INT NOT NULL,
        SalesRepId INT NOT NULL,
        InvoiceDate DATETIME2 NOT NULL,
        DueDate DATETIME2 NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        Currency NVARCHAR(10) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        PaymentMethod NVARCHAR(50) NOT NULL,
        CONSTRAINT FK_Invoices_Customers
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
        CONSTRAINT FK_Invoices_SalesReps
            FOREIGN KEY (SalesRepId) REFERENCES dbo.SalesReps (Id)
    );
    GO

    INSERT INTO dbo.Customers (Name)
    VALUES
    ('Contoso Ltd'),
    ('Northwind Traders'),
    ('Fabrikam'),
    ('Adventure Works'),
    ('Tailspin Toys'),
    ('Blue Yonder'),
    ('Woodgrove Bank'),
    ('Lucerne Publishing');
    GO

    INSERT INTO dbo.SalesReps (Name, Region)
    VALUES
    ('Alice Johnson', 'North America'),
    ('Bob Smith', 'North America'),
    ('Carlos Garcia', 'Europe'),
    ('Diana Lee', 'North America'),
    ('Ethan Wong', 'APAC'),
    ('Fiona Patel', 'Europe'),
    ('Greg Davis', 'North America'),
    ('Hannah Nguyen', 'Europe');
    GO

    INSERT INTO dbo.Invoices
        (CustomerId, SalesRepId, InvoiceDate, DueDate, Amount, Currency, Status, PaymentMethod)
    VALUES
    (1, 1, '2026-01-15', '2026-02-15', 1250.00, 'USD', 'Paid', 'Credit Card'),
    (1, 1, '2026-03-15', '2026-04-15', 875.50, 'USD', 'Pending', 'ACH'),
    (2, 2, '2026-01-16', '2026-02-16', 950.00, 'USD', 'Pending', 'Bank Transfer'),
    (3, 3, '2026-02-02', '2026-03-02', 2450.50, 'EUR', 'Paid', 'Wire Transfer'),
    (4, 4, '2026-02-05', '2026-03-05', 4300.00, 'USD', 'Overdue', 'ACH'),
    (5, 5, '2026-02-20', '2026-03-20', 680.25, 'USD', 'Pending', 'Credit Card'),
    (6, 6, '2026-02-27', '2026-03-27', 1750.75, 'GBP', 'Paid', 'Bank Transfer'),
    (7, 7, '2026-03-01', '2026-04-01', 9800.00, 'USD', 'Paid', 'Wire Transfer'),
    (8, 8, '2026-03-03', '2026-04-03', 3100.00, 'USD', 'Overdue', 'ACH'),
    (2, 2, '2026-03-16', '2026-04-16', 1275.00, 'USD', 'Approved', 'Bank Transfer'),
    (4, 4, '2026-03-20', '2026-04-20', 2150.00, 'USD', 'Draft', 'Credit Card');
    GO
    """;
}