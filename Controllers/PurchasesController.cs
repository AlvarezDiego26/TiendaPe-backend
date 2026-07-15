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
        if (request.Items.Count == 0 || request.Items.Any(x => x.Quantity <= 0 || x.UnitCost < 0 || x.TotalCost.GetValueOrDefault() < 0))
        {
            return BadRequest("La compra debe tener productos validos.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products
            .Include(x => x.Presentations.Where(p => p.IsActive))
            .Where(x => productIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        Supplier? supplier = null;
        if (request.SupplierId.HasValue)
        {
            supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == request.SupplierId.Value && x.IsActive, cancellationToken);
            if (supplier is null)
            {
                return BadRequest("Proveedor no encontrado.");
            }
        }

        var supplierName = supplier?.Name ?? (string.IsNullOrWhiteSpace(request.Supplier) ? "Proveedor" : request.Supplier.Trim());
        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            Supplier = supplierName,
            SupplierId = supplier?.Id,
            PaymentMethod = Clean(request.PaymentMethod) ?? "cash",
            Notes = Clean(request.Notes)
        };

        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return BadRequest($"Producto no encontrado: {item.ProductId}");
            }

            var presentation = ResolvePresentation(product, item.PresentationId, forPurchase: true);
            var unitsPerPackage = item.UnitsPerPackage ?? presentation?.QuantityInBaseUnit ?? product.UnitsPerPackage;
            if (unitsPerPackage <= 0)
            {
                unitsPerPackage = 1;
            }

            var quantityInput = item.Quantity;
            var quantityBase = quantityInput * unitsPerPackage;
            var totalCost = item.TotalCost ?? item.UnitCost * item.Quantity;
            var unitCostBase = quantityBase > 0 ? Math.Round(totalCost / quantityBase, 4) : item.UnitCost;

            var previousStockBase = product.StockBase > 0 ? product.StockBase : product.Stock;
            product.StockBase = previousStockBase + quantityBase;
            product.Stock = ToLegacyStock(product.StockBase);
            product.PurchasePrice = unitCostBase;
            product.AverageCostBase = CalculateWeightedAverageCost(previousStockBase, product.AverageCostBase, quantityBase, unitCostBase);
            product.SuggestedPrice = item.SuggestedPrice ?? product.SuggestedPrice;
            product.ProfitMarginPercent = item.ProfitMarginPercent ?? product.ProfitMarginPercent;
            product.UpdatedAt = DateTime.UtcNow;

            purchase.Total += totalCost;
            purchase.PurchaseItems.Add(new PurchaseItem
            {
                ProductId = product.Id,
                PresentationId = presentation?.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                QuantityBase = quantityBase,
                InputUnit = Clean(item.InputUnit) ?? presentation?.UnitLabel ?? product.PurchaseUnit ?? product.BaseUnit,
                UnitsPerPackage = unitsPerPackage,
                TotalCost = totalCost,
                UnitCostBase = unitCostBase,
                SuggestedPrice = item.SuggestedPrice,
                ProfitMarginPercent = item.ProfitMarginPercent,
                Subtotal = totalCost
            });

            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                ProductName = product.Name,
                MovementType = InventoryMovementType.Entry,
                Quantity = ToLegacyStock(quantityBase),
                Reason = $"Compra a {supplierName}"
            });

            db.InventoryMovementLogs.Add(new InventoryMovementLog
            {
                ProductId = product.Id,
                PresentationId = presentation?.Id,
                Reason = InventoryMovementReason.Purchase,
                QuantityInput = quantityInput,
                InputUnit = Clean(item.InputUnit) ?? presentation?.UnitLabel ?? product.PurchaseUnit ?? product.BaseUnit,
                QuantityBase = quantityBase,
                UnitCostBase = unitCostBase,
                TotalCost = totalCost,
                ReferenceTable = "purchases",
                ReferenceId = purchase.Id,
                Notes = $"Compra a {supplierName}"
            });
        }

        purchase.PaidAmount = request.PaidAmount ?? purchase.Total;
        purchase.PendingAmount = Math.Max(0, purchase.Total - purchase.PaidAmount);

        if (supplier is not null)
        {
            supplier.LastPurchaseAt = purchase.OccurredAt == default ? DateTime.UtcNow : purchase.OccurredAt;
            supplier.TotalPurchased += purchase.Total;
            supplier.PendingBalance += purchase.PendingAmount;
            supplier.UpdatedAt = DateTime.UtcNow;
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

    private static ProductPresentation? ResolvePresentation(Product product, Guid? presentationId, bool forPurchase)
    {
        if (presentationId.HasValue)
        {
            return product.Presentations.FirstOrDefault(x => x.Id == presentationId.Value && x.IsActive);
        }

        return product.Presentations
            .Where(x => x.IsActive && (forPurchase ? x.PurchaseEnabled : x.SaleEnabled))
            .OrderByDescending(x => forPurchase ? x.IsDefaultPurchase : x.IsDefaultSale)
            .FirstOrDefault();
    }

    private static decimal CalculateWeightedAverageCost(decimal previousStock, decimal previousCost, decimal addedStock, decimal addedCost)
    {
        if (previousStock <= 0)
        {
            return addedCost;
        }

        var totalStock = previousStock + addedStock;
        if (totalStock <= 0)
        {
            return addedCost;
        }

        return Math.Round(((previousStock * previousCost) + (addedStock * addedCost)) / totalStock, 4);
    }

    private static PurchaseResponse ToResponse(Purchase purchase) => new(
        purchase.Id,
        purchase.OccurredAt,
        purchase.Supplier,
        purchase.Total,
        purchase.SupplierId,
        purchase.PaymentMethod,
        purchase.PaidAmount,
        purchase.PendingAmount,
        purchase.Notes,
        purchase.PurchaseItems.Select(x => new PurchaseItemResponse(
            x.ProductId,
            x.ProductName,
            x.Quantity,
            x.UnitCost,
            x.Subtotal,
            x.PresentationId,
            x.QuantityBase,
            x.InputUnit,
            x.UnitsPerPackage,
            x.UnitCostBase,
            x.SuggestedPrice,
            x.ProfitMarginPercent)).ToList());

    private static int ToLegacyStock(decimal stockBase) => stockBase <= 0 ? 0 : (int)Math.Floor(stockBase);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
