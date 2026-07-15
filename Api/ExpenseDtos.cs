namespace TiendaPe.Api;

public sealed record CreateExpenseRequest(
    string Category,
    string Description,
    decimal Amount,
    string PaymentMethod,
    bool IsRecurring,
    short? DueDay,
    DateTime? RecurringStart,
    DateTime? RecurringEnd,
    Guid? SupplierId = null,
    bool IsSupplierPayment = false);

public sealed record ExpenseResponse(
    Guid Id,
    DateTime OccurredAt,
    string Category,
    string Description,
    decimal Amount,
    string PaymentMethod,
    Guid? CashSessionId,
    bool IsRecurring,
    short? DueDay,
    DateTime? RecurringStart,
    DateTime? RecurringEnd,
    Guid? SupplierId,
    bool IsSupplierPayment);

public sealed record RecurringPendingExpenseResponse(string Category, string Description, short DueDay);
