namespace DataApiBuilder.AppHost;

internal static class DatabaseScript
{
    public static string CreateDatabaseScript(string db) => $"""
    CREATE DATABASE [{db}];
    GO

    CREATE TABLE dbo.Invoices (
        InvoiceId INT IDENTITY(1,1) PRIMARY KEY,
        CustomerName NVARCHAR(200) NOT NULL,
        InvoiceDate DATETIME2 NOT NULL,
        DueDate DATETIME2 NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        Currency NVARCHAR(10) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        Region NVARCHAR(100) NOT NULL,
        SalesRep NVARCHAR(150) NOT NULL,
        PaymentMethod NVARCHAR(50) NOT NULL
    );
    GO

    INSERT INTO dbo.Invoices (CustomerName, InvoiceDate, DueDate, Amount, Currency, Status, Region, SalesRep, PaymentMethod)
    VALUES
    ('Contoso Ltd', '2026-01-15', '2026-02-15', 1250.00, 'USD', 'Paid', 'North America', 'Alice Johnson', 'Credit Card'),
    ('Northwind Traders', '2026-01-16', '2026-02-16', 950.00, 'USD', 'Pending', 'North America', 'Bob Smith', 'Bank Transfer'),
    ('Fabrikam', '2026-02-02', '2026-03-02', 2450.50, 'EUR', 'Paid', 'Europe', 'Carlos Garcia', 'Wire Transfer'),
    ('Adventure Works', '2026-02-05', '2026-03-05', 4300.00, 'USD', 'Overdue', 'North America', 'Diana Lee', 'ACH'),
    ('Tailspin Toys', '2026-02-20', '2026-03-20', 680.25, 'USD', 'Pending', 'APAC', 'Ethan Wong', 'Credit Card'),
    ('Blue Yonder', '2026-02-27', '2026-03-27', 1750.75, 'GBP', 'Paid', 'Europe', 'Fiona Patel', 'Bank Transfer'),
    ('Woodgrove Bank', '2026-03-01', '2026-04-01', 9800.00, 'USD', 'Paid', 'North America', 'Greg Davis', 'Wire Transfer'),
    ('Lucerne Publishing', '2026-03-03', '2026-04-03', 3100.00, 'USD', 'Overdue', 'Europe', 'Hannah Nguyen', 'ACH');
    GO
    """;
}