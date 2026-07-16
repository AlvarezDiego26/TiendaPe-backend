using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaPe.Api;
using TiendaPe.Domain.Entities;
using TiendaPe.Infrastructure.Persistence;

namespace TiendaPe.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory-movements")]
public sealed class InventoryMovementsController(TiendaPeDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventoryMovementResponse>>> Get(
        [FromQuery] Guid? productId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 80,
        CancellationToken cancellationToken = default)
    {
        var query = db.InventoryMovementLogs
            .Include(x => x.Product)
            .AsNoTracking()
            .AsQueryable();

        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
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

    private static InventoryMovementResponse ToResponse(InventoryMovementLog movement) => new(
        movement.Id,
        movement.ProductId,
        movement.Product.Name,
        movement.PresentationId,
        ToApiValue(movement.Reason),
        movement.QuantityInput,
        movement.InputUnit,
        movement.QuantityBase,
        movement.UnitCostBase,
        movement.TotalCost,
        movement.ReferenceTable,
        movement.ReferenceId,
        movement.Notes,
        movement.OccurredAt);

    private static string ToApiValue(InventoryMovementReason reason) => reason switch
    {
        InventoryMovementReason.Purchase => "purchase",
        InventoryMovementReason.Sale => "sale",
        InventoryMovementReason.Adjustment => "adjustment",
        InventoryMovementReason.Return => "return",
        InventoryMovementReason.InitialStock => "initial_stock",
        InventoryMovementReason.Waste => "waste",
        _ => reason.ToString()
    };
}

