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
    decimal YapeSales,
    decimal PlinSales,
    decimal TransferSales,
    decimal CashExpenses,
    decimal DigitalExpenses,
    decimal SupplierPayments,
    decimal PersonalWithdrawals,
    decimal? FinalCash,
    bool HasNegativeStreakAlert);
