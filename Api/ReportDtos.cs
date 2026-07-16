namespace TiendaPe.Api;

public sealed record SummaryResponse(
    decimal Income,
    decimal Expenses,
    decimal CostOfGoodsSold,
    decimal NetProfit,
    decimal CashSales,
    decimal DigitalSales,
    decimal YapeSales,
    decimal PlinSales,
    decimal TransferSales);

public sealed record TopProductResponse(Guid ProductId, string ProductName, int Quantity, decimal Income, decimal QuantityBase = 0);
public sealed record StagnantProductResponse(Guid ProductId, string Name, int Stock, DateTime? LastSaleAt);
public sealed record InventoryValueResponse(decimal Value);
