namespace TiendaPe.Api;

public sealed record PurchaseItemRequest(Guid ProductId, int Quantity, decimal UnitCost);
public sealed record CreatePurchaseRequest(string? Supplier, IReadOnlyList<PurchaseItemRequest> Items);

public sealed record PurchaseItemResponse(Guid ProductId, string ProductName, int Quantity, decimal UnitCost, decimal Subtotal);

public sealed record PurchaseResponse(
    Guid Id,
    DateTime OccurredAt,
    string Supplier,
    decimal Total,
    IReadOnlyList<PurchaseItemResponse> Items);
