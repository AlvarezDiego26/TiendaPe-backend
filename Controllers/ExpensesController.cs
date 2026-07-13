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

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into expenses (category, description, amount, payment_method, cash_session_id, is_recurring, due_day, recurring_start, recurring_end)
            values (
                @category,
                @description,
                @amount,
                cast(@payment_method as payment_method),
                (select id from cash_sessions where closed_at is null order by opened_at desc limit 1),
                @is_recurring,
                @due_day,
                @recurring_start,
                @recurring_end
            )
            returning id, occurred_at, category, description, amount, payment_method::text, cash_session_id, is_recurring, due_day, recurring_start, recurring_end;
            """;

        AddParameter(command, "category", category.ToApiValue());
        AddParameter(command, "description", request.Description.Trim());
        AddParameter(command, "amount", request.Amount, System.Data.DbType.Decimal);
        AddParameter(command, "payment_method", paymentMethod.ToApiValue());
        AddParameter(command, "is_recurring", request.IsRecurring, System.Data.DbType.Boolean);
        AddParameter(command, "due_day", request.IsRecurring ? request.DueDay : null, System.Data.DbType.Int16);
        AddParameter(command, "recurring_start", request.IsRecurring ? request.RecurringStart?.Date : null, System.Data.DbType.Date);
        AddParameter(command, "recurring_end", request.IsRecurring ? request.RecurringEnd?.Date : null, System.Data.DbType.Date);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return Ok(new ExpenseResponse(
            reader.GetGuid(0),
            reader.GetDateTime(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDecimal(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.GetBoolean(7),
            reader.IsDBNull(8) ? null : reader.GetInt16(8),
            reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            reader.IsDBNull(10) ? null : reader.GetDateTime(10)));
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
        expense.RecurringEnd);

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
