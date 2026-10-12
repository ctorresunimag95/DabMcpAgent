namespace DataApiBuilder.AppHost;

internal static class DatabaseScript
{
    public static string CreateDatabaseScript(string db) => $"""
    CREATE DATABASE [{db}];
    GO

    CREATE TABLE dbo.Invoice (
        InvoiceId INT IDENTITY(1,1) PRIMARY KEY,
        CustomerId INT NOT NULL,
        CustomerName NVARCHAR(200) NOT NULL,
        SalesRepId INT NOT NULL,
        SalesRepName NVARCHAR(150) NOT NULL,
        SalesRepRegion NVARCHAR(100) NOT NULL,
        InvoiceDate DATETIME2 NOT NULL,
        DueDate DATETIME2 NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        Currency NVARCHAR(10) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        PaymentMethod NVARCHAR(50) NOT NULL
    );
    GO

    INSERT INTO dbo.Invoice
        (CustomerId, CustomerName, SalesRepId, SalesRepName, SalesRepRegion,
         InvoiceDate, DueDate, Amount, Currency, Status, PaymentMethod)
    VALUES
    (1, 'Contoso Ltd', 1, 'Alice Johnson', 'North America', '2026-01-15', '2026-02-15', 1250.00, 'USD', 'Paid', 'Credit Card'),
    (1, 'Contoso Ltd', 1, 'Alice Johnson', 'North America', '2026-03-15', '2026-04-15', 875.50, 'USD', 'Pending', 'ACH'),
    (2, 'Northwind Traders', 2, 'Bob Smith', 'North America', '2026-01-16', '2026-02-16', 950.00, 'USD', 'Pending', 'Bank Transfer'),
    (3, 'Fabrikam', 3, 'Carlos Garcia', 'Europe', '2026-02-02', '2026-03-02', 2450.50, 'EUR', 'Paid', 'Wire Transfer'),
    (4, 'Adventure Works', 4, 'Diana Lee', 'North America', '2026-02-05', '2026-03-05', 4300.00, 'USD', 'Overdue', 'ACH'),
    (5, 'Tailspin Toys', 5, 'Ethan Wong', 'APAC', '2026-02-20', '2026-03-20', 680.25, 'USD', 'Pending', 'Credit Card'),
    (6, 'Blue Yonder', 6, 'Fiona Patel', 'Europe', '2026-02-27', '2026-03-27', 1750.75, 'GBP', 'Paid', 'Bank Transfer'),
    (7, 'Woodgrove Bank', 7, 'Greg Davis', 'North America', '2026-03-01', '2026-04-01', 9800.00, 'USD', 'Paid', 'Wire Transfer'),
    (8, 'Lucerne Publishing', 8, 'Hannah Nguyen', 'Europe', '2026-03-03', '2026-04-03', 3100.00, 'USD', 'Overdue', 'ACH'),
    (2, 'Northwind Traders', 2, 'Bob Smith', 'North America', '2026-03-16', '2026-04-16', 1275.00, 'USD', 'Approved', 'Bank Transfer'),
    (4, 'Adventure Works', 4, 'Diana Lee', 'North America', '2026-03-20', '2026-04-20', 2150.00, 'USD', 'Draft', 'Credit Card');
    GO
    """;
}