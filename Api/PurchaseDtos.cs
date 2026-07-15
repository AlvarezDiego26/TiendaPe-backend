namespace TiendaPe.Api;

public sealed record PurchaseItemRequest(
    Guid ProductId,
    int Quantity,
    decimal UnitCost,
    Guid? PresentationId = null,
    string? InputUnit = null,
    decimal? UnitsPerPackage = null,
    decimal? TotalCost = null,
    decimal? SuggestedPrice = null,
    decimal? ProfitMarginPercent = null);

public sealed record CreatePurchaseRequest(
    string? Supplier,
    IReadOnlyList<PurchaseItemRequest> Items,
    Guid? SupplierId = null,
    string? PaymentMethod = null,
    decimal? PaidAmount = null,
    string? Notes = null);

public sealed record PurchaseItemResponse(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitCost,
    decimal Subtotal,
    Guid? PresentationId,
    decimal QuantityBase,
    string? InputUnit,
    decimal UnitsPerPackage,
    decimal? UnitCostBase,
    decimal? SuggestedPrice,
    decimal? ProfitMarginPercent);

public sealed record PurchaseResponse(
    Guid Id,
    DateTime OccurredAt,
    string Supplier,
    decimal Total,
    Guid? SupplierId,
    string PaymentMethod,
    decimal PaidAmount,
    decimal PendingAmount,
    string? Notes,
    IReadOnlyList<PurchaseItemResponse> Items);
