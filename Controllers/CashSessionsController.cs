using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaPe.Api;
using TiendaPe.Domain.Entities;
using TiendaPe.Infrastructure.Persistence;

namespace TiendaPe.Controllers;

[ApiController]
[Authorize]
[Route("api/cash-sessions")]
public sealed class CashSessionsController(TiendaPeDbContext db) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<CashSessionResponse>> Current(CancellationToken cancellationToken)
    {
        var session = await GetCurrentResponse(cancellationToken);
        return session is null ? NotFound("No hay turno de caja abierto.") : Ok(session);
    }

    [HttpPost("open")]
    public async Task<ActionResult<CashSessionResponse>> Open(OpenCashSessionRequest request, CancellationToken cancellationToken)
    {
        if (request.OpeningAmount < 0)
        {
            return BadRequest("El monto inicial no puede ser negativo.");
        }

        if (await db.CashSessions.AnyAsync(x => x.ClosedAt == null, cancellationToken))
        {
            var current = await GetCurrentResponse(cancellationToken);
            return Conflict(current is null ? "Ya existe un turno de caja abierto." : current);
        }

        var session = new CashSession
        {
            OpeningAmount = request.OpeningAmount,
            OpenedById = CurrentUserId()
        };

        db.CashSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Current), ToResponse(session, false));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<CashSessionResponse>> Close(Guid id, CloseCashSessionRequest request, CancellationToken cancellationToken)
    {
        if (request.CountedAmount < 0)
        {
            return BadRequest("El monto contado no puede ser negativo.");
        }

        if (id == Guid.Empty)
        {
            id = await db.CashSessions
                .Where(x => x.ClosedAt == null)
                .OrderByDescending(x => x.OpenedAt)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (id == Guid.Empty)
            {
                return NotFound("No hay un turno de caja abierto para cerrar.");
            }
        }

        var session = await db.CashSessions
            .Include(x => x.Sales)
            .Include(x => x.Expenses)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (session is null || session.ClosedAt is not null)
        {
            var openSession = await db.CashSessions
                .Include(x => x.Sales)
                .Include(x => x.Expenses)
                .AsSplitQuery()
                .Where(x => x.ClosedAt == null)
                .OrderByDescending(x => x.OpenedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (openSession is not null)
            {
                session = openSession;
            }
        }

        if (session is null)
        {
            return NotFound();
        }

        if (session.ClosedAt is not null)
        {
            return Conflict("Este turno ya está cerrado.");
        }

        var cashSales = session.Sales.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.Total);
        var cashExpenses = session.Expenses.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.Amount);
        var expected = session.OpeningAmount + cashSales - cashExpenses;

        session.ExpectedAmount = expected;
        session.CountedAmount = request.CountedAmount;
        session.Difference = request.CountedAmount - expected;
        session.ClosedAt = DateTime.UtcNow;
        session.ClosedById = CurrentUserId();

        var otherOpenSessions = await db.CashSessions
            .Include(x => x.Sales)
            .Include(x => x.Expenses)
            .AsSplitQuery()
            .Where(x => x.ClosedAt == null && x.Id != session.Id)
            .ToListAsync(cancellationToken);

        foreach (var openSession in otherOpenSessions)
        {
            var openCashSales = openSession.Sales.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.Total);
            var openCashExpenses = openSession.Expenses.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.Amount);
            var openExpected = openSession.OpeningAmount + openCashSales - openCashExpenses;

            openSession.ExpectedAmount = openExpected;
            openSession.CountedAmount = openExpected;
            openSession.Difference = 0;
            openSession.ClosedAt = session.ClosedAt;
            openSession.ClosedById = session.ClosedById;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(session, session.Difference < 0));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CashSessionResponse>>> History(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var query = db.CashSessions
            .Include(x => x.Sales)
            .Include(x => x.Expenses)
            .AsSplitQuery()
            .AsNoTracking()
            .Where(x => x.ClosedAt != null);

        if (from.HasValue)
        {
            query = query.Where(x => x.OpenedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.OpenedAt <= to.Value);
        }

        var sessions = await query.OrderByDescending(x => x.OpenedAt).ToListAsync(cancellationToken);
        var negativeStreak = sessions.Take(3).Count() == 3 && sessions.Take(3).All(x => x.Difference < 0);

        return Ok(sessions.Select(x => ToResponse(x, negativeStreak)).ToList());
    }

    private Guid? CurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }

    private async Task<CashSessionResponse?> GetCurrentResponse(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 3;
        command.CommandText = """
            select
                c.id,
                c.opened_at,
                c.closed_at,
                c.opening_amount,
                c.expected_amount,
                c.counted_amount,
                c.difference,
                coalesce((select sum(s.total) from sales s where s.cash_session_id = c.id and s.payment_method = 'cash'), 0) as cash_sales,
                coalesce((select sum(s.total) from sales s where s.cash_session_id = c.id and s.payment_method = 'yape_plin'), 0) as digital_sales,
                coalesce((select sum(e.amount) from expenses e where e.cash_session_id = c.id and e.payment_method = 'cash'), 0) as cash_expenses,
                coalesce((select sum(e.amount) from expenses e where e.cash_session_id = c.id and e.payment_method = 'yape_plin'), 0) as digital_expenses
            from cash_sessions c
            where c.closed_at is null
            order by c.opened_at desc
            limit 1;
            """;

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new CashSessionResponse(
                reader.GetGuid(0),
                reader.GetDateTime(1),
                reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.GetDecimal(10),
                false);
        }
        catch (Exception ex) when (ex is TimeoutException || ex.InnerException is TimeoutException)
        {
            return await db.CashSessions.AnyAsync(x => x.ClosedAt == null, cancellationToken)
                ? new CashSessionResponse(Guid.Empty, DateTime.UtcNow, null, 0, null, null, null, 0, 0, 0, 0, false)
                : null;
        }
    }

    private static CashSessionResponse ToResponse(CashSession session, bool hasNegativeStreakAlert)
    {
        var cashSales = session.Sales.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.Total);
        var digitalSales = session.Sales.Where(x => x.PaymentMethod == PaymentMethod.YapePlin).Sum(x => x.Total);
        var cashExpenses = session.Expenses.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.Amount);
        var digitalExpenses = session.Expenses.Where(x => x.PaymentMethod == PaymentMethod.YapePlin).Sum(x => x.Amount);

        return new CashSessionResponse(
            session.Id,
            session.OpenedAt,
            session.ClosedAt,
            session.OpeningAmount,
            session.ExpectedAmount,
            session.CountedAmount,
            session.Difference,
            cashSales,
            digitalSales,
            cashExpenses,
            digitalExpenses,
            hasNegativeStreakAlert);
    }
}
