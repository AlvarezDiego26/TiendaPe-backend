using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaPe.Api;
using TiendaPe.Domain.Entities;
using TiendaPe.Infrastructure.Persistence;

namespace TiendaPe.Controllers;

[ApiController]
[Authorize]
[Route("api/purchases")]
public sealed class PurchasesController(TiendaPeDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PurchaseResponse>> Create(CreatePurchaseRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0 || request.Items.Any(x => x.Quantity <= 0 || x.UnitCost < 0))
        {
            return BadRequest("La compra debe tener productos válidos.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(x => productIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var purchase = new Purchase
        {
            Supplier = string.IsNullOrWhiteSpace(request.Supplier) ? "Proveedor" : request.Supplier.Trim()
        };

        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return BadRequest($"Producto no encontrado: {item.ProductId}");
            }

            var subtotal = item.UnitCost * item.Quantity;
            product.Stock += item.Quantity;
            product.PurchasePrice = item.UnitCost;
            purchase.Total += subtotal;
            purchase.PurchaseItems.Add(new PurchaseItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                Subtotal = subtotal
            });

            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                ProductName = product.Name,
                MovementType = InventoryMovementType.Entry,
                Quantity = item.Quantity,
                Reason = $"Compra a {purchase.Supplier}"
            });
        }

        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = purchase.Id }, ToResponse(purchase));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var purchase = await db.Purchases
            .Include(x => x.PurchaseItems)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return purchase is null ? NotFound() : Ok(ToResponse(purchase));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PurchaseResponse>>> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var query = db.Purchases.Include(x => x.PurchaseItems).AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(x => x.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.OccurredAt <= to.Value);
        }

        var purchases = await query.OrderByDescending(x => x.OccurredAt).ToListAsync(cancellationToken);
        return Ok(purchases.Select(ToResponse).ToList());
    }

    private static PurchaseResponse ToResponse(Purchase purchase) => new(
        purchase.Id,
        purchase.OccurredAt,
        purchase.Supplier,
        purchase.Total,
        purchase.PurchaseItems.Select(x => new PurchaseItemResponse(x.ProductId, x.ProductName, x.Quantity, x.UnitCost, x.Subtotal)).ToList());
}
