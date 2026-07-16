using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaPe.Api;
using TiendaPe.Infrastructure.Persistence;
using TiendaPe.Domain.Entities;

namespace TiendaPe.Controllers;

[ApiController]
[Authorize]
[Route("api/cash-movements")]
public sealed class CashMovementsController(TiendaPeDbContext db) : ControllerBase
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "supplier_payment",
        "personal_withdrawal",
        "cash_adjustment",
        "other"
    };

    [HttpPost]
    public async Task<ActionResult<CashMovementResponse>> Create(CreateCashMovementRequest request, CancellationToken cancellationToken)
    {
        var type = request.Type.Trim().ToLowerInvariant();
        if (!AllowedTypes.Contains(type))
        {
            return BadRequest("Tipo de movimiento de caja invalido.");
        }

        if (!ApiEnums.TryParsePaymentMethod(request.PaymentMethod, out var paymentMethod))
        {
            return BadRequest("Metodo de pago invalido.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("El monto debe ser mayor a cero.");
        }

        var session = await db.CashSessions
            .Where(x => x.ClosedAt == null)
            .OrderByDescending(x => x.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return BadRequest("Abre un turno de caja antes de registrar movimientos.");
        }

        var movement = new CashMovement
        {
            CashSessionId = session.Id,
            Type = type,
            PaymentMethod = paymentMethod.ToApiValue(),
            Amount = request.Amount,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ReferenceTable = request.SupplierId.HasValue ? "suppliers" : null,
            ReferenceId = request.SupplierId
        };

        if (type == "supplier_payment")
        {
            session.SupplierPayments += request.Amount;
        }
        else if (type == "personal_withdrawal")
        {
            session.PersonalWithdrawals += request.Amount;
        }

        db.CashMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(movement));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CashMovementResponse>>> Get(
        [FromQuery] Guid? cashSessionId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 80,
        CancellationToken cancellationToken = default)
    {
        var query = db.CashMovements.AsNoTracking().AsQueryable();

        if (cashSessionId.HasValue)
        {
            query = query.Where(x => x.CashSessionId == cashSessionId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.OccurredAt <= to.Value);
        }

        var movements = await query
            .OrderByDescending(x => x.OccurredAt)
            .Take(Math.Clamp(limit, 1, 300))
            .ToListAsync(cancellationToken);

        return Ok(movements.Select(ToResponse).ToList());
    }

    private static CashMovementResponse ToResponse(CashMovement movement) => new(
        movement.Id,
        movement.CashSessionId,
        movement.Type,
        movement.PaymentMethod,
        movement.Amount,
        movement.Description,
        movement.ReferenceTable,
        movement.ReferenceId,
        movement.OccurredAt);
}

