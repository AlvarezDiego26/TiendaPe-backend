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
            return BadRequest("Metodo de pago invalido.");
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
            .GroupBy(x => new { x.ProductId, x.PresentationId })
            .Select(x => new SaleItemRequest(x.Key.ProductId, x.Sum(i => i.Quantity), x.Key.PresentationId))
            .ToList();

        var productIds = groupedItems.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products
            .Include(x => x.Presentations.Where(p => p.IsActive))
            .Where(x => productIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var item in groupedItems)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return BadRequest($"Producto no encontrado: {item.ProductId}");
            }

            var presentation = ResolveSalePresentation(product, item.PresentationId);
            var quantityBase = item.Quantity * (presentation?.QuantityInBaseUnit ?? 1);
            var available = product.StockBase > 0 ? product.StockBase : product.Stock;
            if (available < quantityBase)
            {
                return BadRequest($"Stock insuficiente para {product.Name}. Disponible: {available:0.###} {product.BaseUnit}.");
            }
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            PaymentMethod = paymentMethod,
            CashSessionId = cashSession.Id
        };

        foreach (var item in groupedItems)
        {
            var product = products[item.ProductId];
            var presentation = ResolveSalePresentation(product, item.PresentationId);
            var unitPrice = presentation?.SalePrice ?? product.SalePrice;
            var quantityBase = item.Quantity * (presentation?.QuantityInBaseUnit ?? 1);
            var inputUnit = presentation?.UnitLabel ?? product.SaleUnit ?? product.BaseUnit;
            var unitCostBase = product.AverageCostBase > 0 ? product.AverageCostBase : product.PurchasePrice;
            var subtotal = unitPrice * item.Quantity;
            var profit = subtotal - (unitCostBase * quantityBase);

            product.StockBase = Math.Max(0, (product.StockBase > 0 ? product.StockBase : product.Stock) - quantityBase);
            product.Stock = ToLegacyStock(product.StockBase);
            product.UpdatedAt = DateTime.UtcNow;

            sale.Total += subtotal;
            sale.SaleItems.Add(new SaleItem
            {
                ProductId = product.Id,
                PresentationId = presentation?.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                Subtotal = subtotal,
                QuantityBase = quantityBase,
                InputUnit = inputUnit,
                UnitCostBase = unitCostBase,
                Profit = profit
            });

            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                ProductName = product.Name,
                MovementType = InventoryMovementType.Sale,
                Quantity = ToLegacyStock(quantityBase),
                Reason = $"Venta {paymentMethod.ToApiValue()}"
            });

            db.InventoryMovementLogs.Add(new InventoryMovementLog
            {
                ProductId = product.Id,
                PresentationId = presentation?.Id,
                Reason = InventoryMovementReason.Sale,
                QuantityInput = item.Quantity,
                InputUnit = inputUnit,
                QuantityBase = -quantityBase,
                UnitCostBase = unitCostBase,
                TotalCost = unitCostBase * quantityBase,
                ReferenceTable = "sales",
                ReferenceId = sale.Id,
                Notes = $"Venta {paymentMethod.ToApiValue()}"
            });
        }

        if (paymentMethod == PaymentMethod.Cash)
        {
            cashSession.CashSales += sale.Total;
        }
        else if (paymentMethod == PaymentMethod.Yape)
        {
            cashSession.YapeSales += sale.Total;
        }
        else if (paymentMethod == PaymentMethod.Plin)
        {
            cashSession.PlinSales += sale.Total;
        }
        else if (paymentMethod == PaymentMethod.Transfer)
        {
            cashSession.TransferSales += sale.Total;
        }
        else
        {
            cashSession.YapeSales += sale.Total;
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

    private static ProductPresentation? ResolveSalePresentation(Product product, Guid? presentationId)
    {
        if (presentationId.HasValue)
        {
            return product.Presentations.FirstOrDefault(x => x.Id == presentationId.Value && x.IsActive);
        }

        return product.Presentations
            .Where(x => x.IsActive && x.SaleEnabled)
            .OrderByDescending(x => x.IsDefaultSale)
            .FirstOrDefault();
    }

    private static SaleResponse ToResponse(Sale sale) => new(
        sale.Id,
        sale.OccurredAt,
        sale.PaymentMethod.ToApiValue(),
        sale.Total,
        sale.CashSessionId,
        sale.SaleItems.Select(x => new SaleItemResponse(
            x.ProductId,
            x.ProductName,
            x.Quantity,
            x.UnitPrice,
            x.Subtotal,
            x.PresentationId,
            x.QuantityBase,
            x.InputUnit,
            x.UnitCostBase,
            x.Profit)).ToList());

    private static int ToLegacyStock(decimal stockBase) => stockBase <= 0 ? 0 : (int)Math.Floor(stockBase);
}
