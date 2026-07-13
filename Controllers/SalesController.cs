using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaPe.Api;
using TiendaPe.Domain.Entities;
using TiendaPe.Infrastructure.Persistence;

namespace TiendaPe.Controllers;

[ApiController]
[Authorize]
[Route("api/sales")]
public sealed class SalesController(TiendaPeDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SaleResponse>> Create(CreateSaleRequest request, CancellationToken cancellationToken)
    {
        if (!ApiEnums.TryParsePaymentMethod(request.PaymentMethod, out var paymentMethod))
        {
            return BadRequest("Método de pago inválido.");
        }

        if (request.Items.Count == 0 || request.Items.Any(x => x.Quantity <= 0))
        {
            return BadRequest("La venta debe tener productos con cantidad mayor a cero.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var cashSession = await db.CashSessions
            .Where(x => x.ClosedAt == null)
            .OrderByDescending(x => x.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (cashSession is null)
        {
            return BadRequest("Abre un turno de caja antes de registrar ventas.");
        }

        var groupedItems = request.Items
            .GroupBy(x => x.ProductId)
            .Select(x => new SaleItemRequest(x.Key, x.Sum(i => i.Quantity)))
            .ToList();

        var productIds = groupedItems.Select(x => x.ProductId).ToList();
        var products = await db.Products
            .Where(x => productIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var item in groupedItems)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return BadRequest($"Producto no encontrado: {item.ProductId}");
            }

            if (product.Stock < item.Quantity)
            {
                return BadRequest($"Stock insuficiente para {product.Name}. Disponible: {product.Stock}.");
            }
        }

        var sale = new Sale
        {
            PaymentMethod = paymentMethod,
            CashSessionId = cashSession.Id
        };

        foreach (var item in groupedItems)
        {
            var product = products[item.ProductId];
            var subtotal = product.SalePrice * item.Quantity;

            product.Stock -= item.Quantity;
            sale.Total += subtotal;
            sale.SaleItems.Add(new SaleItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.SalePrice,
                Subtotal = subtotal
            });

            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                ProductName = product.Name,
                MovementType = InventoryMovementType.Sale,
                Quantity = item.Quantity,
                Reason = $"Venta {paymentMethod.ToApiValue()}"
            });
        }

        db.Sales.Add(sale);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = sale.Id }, ToResponse(sale));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SaleResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var sale = await db.Sales
            .Include(x => x.SaleItems)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return sale is null ? NotFound() : Ok(ToResponse(sale));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SaleResponse>>> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var query = db.Sales.Include(x => x.SaleItems).AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(x => x.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.OccurredAt <= to.Value);
        }

        var sales = await query.OrderByDescending(x => x.OccurredAt).ToListAsync(cancellationToken);
        return Ok(sales.Select(ToResponse).ToList());
    }

    private static SaleResponse ToResponse(Sale sale) => new(
        sale.Id,
        sale.OccurredAt,
        sale.PaymentMethod.ToApiValue(),
        sale.Total,
        sale.CashSessionId,
        sale.SaleItems.Select(x => new SaleItemResponse(x.ProductId, x.ProductName, x.Quantity, x.UnitPrice, x.Subtotal)).ToList());
}
