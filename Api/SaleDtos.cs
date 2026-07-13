namespace TiendaPe.Api;

public sealed record SaleItemRequest(Guid ProductId, int Quantity);
public sealed record CreateSaleRequest(string PaymentMethod, IReadOnlyList<SaleItemRequest> Items);

public sealed record SaleItemResponse(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal);

public sealed record SaleResponse(
    Guid Id,
    DateTime OccurredAt,
    string PaymentMethod,
    decimal Total,
    Guid? CashSessionId,
    IReadOnlyList<SaleItemResponse> Items);
