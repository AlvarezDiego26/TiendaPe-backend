namespace TiendaPe.Api;

public sealed record OpenCashSessionRequest(decimal OpeningAmount);
public sealed record CloseCashSessionRequest(decimal CountedAmount);

public sealed record CashSessionResponse(
    Guid Id,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningAmount,
    decimal? ExpectedAmount,
    decimal? CountedAmount,
    decimal? Difference,
    decimal CashSales,
    decimal DigitalSales,
    decimal CashExpenses,
    decimal DigitalExpenses,
    bool HasNegativeStreakAlert);
