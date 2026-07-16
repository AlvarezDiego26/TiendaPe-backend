namespace TiendaPe.Api;

public sealed record CreateCashMovementRequest(
    string Type,
    string PaymentMethod,
    decimal Amount,
    string? Description,
    Guid? SupplierId = null);

public sealed record CashMovementResponse(
    Guid Id,
    Guid? CashSessionId,
    string Type,
    string PaymentMethod,
    decimal Amount,
    string? Description,
    string? ReferenceTable,
    Guid? ReferenceId,
    DateTime OccurredAt);

