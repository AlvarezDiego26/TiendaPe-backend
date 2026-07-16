namespace TiendaPe.Api;

public sealed record InventoryMovementResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid? PresentationId,
    string Reason,
    decimal QuantityInput,
    string InputUnit,
    decimal QuantityBase,
    decimal? UnitCostBase,
    decimal? TotalCost,
    string? ReferenceTable,
    Guid? ReferenceId,
    string? Notes,
    DateTime OccurredAt);

