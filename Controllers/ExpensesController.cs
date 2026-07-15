using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaPe.Api;
using TiendaPe.Domain.Entities;
using TiendaPe.Infrastructure.Persistence;

namespace TiendaPe.Controllers;

[ApiController]
[Authorize]
[Route("api/expenses")]
public sealed class ExpensesController(TiendaPeDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> Create(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        if (!ApiEnums.TryParseExpenseCategory(request.Category, out var category))
        {
            return BadRequest("Categoría inválida.");
        }

        if (!ApiEnums.TryParsePaymentMethod(request.PaymentMethod, out var paymentMethod))
        {
            return BadRequest("Método de pago inválido.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("El gasto debe ser mayor a cero.");
        }

        if (request.IsRecurring && request.DueDay is not >= 1 and <= 31)
        {
            return BadRequest("El día de vencimiento debe estar entre 1 y 31.");
        }

        if (request.IsRecurring && request.RecurringStart.HasValue && request.RecurringEnd.HasValue && request.RecurringEnd < request.RecurringStart)
        {
            return BadRequest("La fecha final no puede ser menor que la fecha inicial.");
        }

        if (request.SupplierId.HasValue && !await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId.Value && x.IsActive, cancellationToken))
        {
            return BadRequest("Proveedor no encontrado.");
        }

        var cashSessionId = await db.CashSessions
            .Where(x => x.ClosedAt == null)
            .OrderByDescending(x => x.OpenedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var expense = new Expense
        {
            Category = category.ToApiValue(),
            Description = request.Description.Trim(),
            Amount = request.Amount,
            PaymentMethod = paymentMethod,
            CashSessionId = cashSessionId,
            IsRecurring = request.IsRecurring,
            DueDay = request.IsRecurring ? request.DueDay : null,
            RecurringStart = request.IsRecurring ? request.RecurringStart?.Date : null,
            RecurringEnd = request.IsRecurring ? request.RecurringEnd?.Date : null,
            SupplierId = request.SupplierId,
            IsSupplierPayment = request.IsSupplierPayment
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(expense));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExpenseResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var expense = await db.Expenses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return expense is null ? NotFound() : Ok(ToResponse(expense));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExpenseResponse>> Update(Guid id, CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        if (!ApiEnums.TryParseExpenseCategory(request.Category, out var category))
        {
            return BadRequest("Categoria invalida.");
        }

        if (!ApiEnums.TryParsePaymentMethod(request.PaymentMethod, out var paymentMethod))
        {
            return BadRequest("Metodo de pago invalido.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("El gasto debe ser mayor a cero.");
        }

        if (request.IsRecurring && request.DueDay is not >= 1 and <= 31)
        {
            return BadRequest("El dia de vencimiento debe estar entre 1 y 31.");
        }

        if (request.IsRecurring && request.RecurringStart.HasValue && request.RecurringEnd.HasValue && request.RecurringEnd < request.RecurringStart)
        {
            return BadRequest("La fecha final no puede ser menor que la fecha inicial.");
        }

        if (request.SupplierId.HasValue && !await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId.Value && x.IsActive, cancellationToken))
        {
            return BadRequest("Proveedor no encontrado.");
        }

        var expense = await db.Expenses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (expense is null)
        {
            return NotFound();
        }

        expense.Category = category.ToApiValue();
        expense.Description = request.Description.Trim();
        expense.Amount = request.Amount;
        expense.PaymentMethod = paymentMethod;
        expense.IsRecurring = request.IsRecurring;
        expense.DueDay = request.IsRecurring ? request.DueDay : null;
        expense.RecurringStart = request.IsRecurring ? request.RecurringStart?.Date : null;
        expense.RecurringEnd = request.IsRecurring ? request.RecurringEnd?.Date : null;
        expense.SupplierId = request.SupplierId;
        expense.IsSupplierPayment = request.IsSupplierPayment;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(expense));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseResponse>>> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        var query = db.Expenses.AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(x => x.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.OccurredAt <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!ApiEnums.TryParseExpenseCategory(category, out var parsedCategory))
            {
                return BadRequest("Categoría inválida.");
            }

            var categoryValue = parsedCategory.ToApiValue();
            query = query.Where(x => x.Category == categoryValue);
        }

        var expenses = await query.OrderByDescending(x => x.OccurredAt).ToListAsync(cancellationToken);
        return Ok(expenses.Select(ToResponse).ToList());
    }

    [HttpGet("recurring-pending")]
    public async Task<ActionResult<IReadOnlyList<RecurringPendingExpenseResponse>>> RecurringPending(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var nextMonth = monthStart.AddMonths(1);

        var recurring = await db.Expenses
            .AsNoTracking()
            .Where(x => x.IsRecurring && x.DueDay != null && x.DueDay <= today.Day)
            .Where(x => x.RecurringStart == null || x.RecurringStart <= today)
            .Where(x => x.RecurringEnd == null || x.RecurringEnd >= monthStart)
            .ToListAsync(cancellationToken);

        var pending = recurring
            .GroupBy(x => new { x.Category, x.Description, x.DueDay })
            .Where(group => !group.Any(x => x.OccurredAt >= monthStart && x.OccurredAt < nextMonth))
            .Select(group => new RecurringPendingExpenseResponse(
                group.Key.Category,
                group.Key.Description,
                group.Key.DueDay!.Value))
            .ToList();

        return Ok(pending);
    }

    private static ExpenseResponse ToResponse(Expense expense) => new(
        expense.Id,
            expense.OccurredAt,
            expense.Category,
        expense.Description,
        expense.Amount,
        expense.PaymentMethod.ToApiValue(),
        expense.CashSessionId,
        expense.IsRecurring,
        expense.DueDay,
        expense.RecurringStart,
        expense.RecurringEnd,
        expense.SupplierId,
        expense.IsSupplierPayment);

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object? value,
        System.Data.DbType? dbType = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        if (dbType.HasValue)
        {
            parameter.DbType = dbType.Value;
        }
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
