namespace TiendaPe.Api;

public sealed record SupplierRequest(
    string Name,
    string? Phone,
    string? Company,
    string? Notes);

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? Phone,
    string? Company,
    string? Notes,
    DateTime? LastPurchaseAt,
    decimal TotalPurchased,
    decimal PendingBalance,
    bool IsActive);
