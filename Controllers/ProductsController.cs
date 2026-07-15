using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaPe.Api;
using TiendaPe.Domain.Entities;
using TiendaPe.Infrastructure.Persistence;

namespace TiendaPe.Controllers;

[ApiController]
[Authorize]
[Route("api/products")]
public sealed class ProductsController(TiendaPeDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> Get(
        [FromQuery] string? search,
        [FromQuery] bool onlyLowStock = false,
        [FromQuery] int limit = 300,
        CancellationToken cancellationToken = default)
    {
        var query = db.Products
            .Include(x => x.Presentations.Where(p => p.IsActive))
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                x.Category.ToLower().Contains(term) ||
                (x.InternalCode != null && x.InternalCode.ToLower().Contains(term)) ||
                (x.Barcode != null && x.Barcode.ToLower().Contains(term)) ||
                (x.Brand != null && x.Brand.ToLower().Contains(term)) ||
                x.Presentations.Any(p => p.Barcode != null && p.Barcode.ToLower().Contains(term)));
        }

        if (onlyLowStock)
        {
            query = query.Where(x =>
                (x.StockBase > 0 || x.MinimumStockBase > 0)
                    ? x.StockBase <= x.MinimumStockBase
                    : x.Stock <= x.MinimumStock);
        }

        var products = await query
            .OrderBy(x => x.Name)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(cancellationToken);

        return Ok(products.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(ProductRequest request, CancellationToken cancellationToken)
    {
        if (!ValidateProductRequest(request, out var error))
        {
            return BadRequest(error);
        }

        if (!ApiEnums.TryParseProductTrackingType(request.TrackingType, out var trackingType))
        {
            return BadRequest("Tipo de control de producto invalido.");
        }

        var stockBase = request.StockBase ?? request.Stock;
        var minimumStockBase = request.MinimumStockBase ?? request.MinimumStock;
        var averageCostBase = request.AverageCostBase ?? request.PurchasePrice;
        var unitsPerPackage = request.UnitsPerPackage.GetValueOrDefault(1);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            InternalCode = Clean(request.InternalCode),
            Barcode = Clean(request.Barcode),
            Brand = Clean(request.Brand),
            Presentation = Clean(request.Presentation),
            Unit = Clean(request.Unit),
            BaseUnit = Clean(request.BaseUnit) ?? Clean(request.Unit) ?? "unidad",
            TrackingType = trackingType,
            ProfitMarginPercent = request.ProfitMarginPercent ?? CalculateMargin(request.PurchasePrice, request.SalePrice),
            SuggestedPrice = request.SuggestedPrice,
            PurchaseUnit = Clean(request.PurchaseUnit) ?? Clean(request.Presentation) ?? Clean(request.Unit) ?? "unidad",
            SaleUnit = Clean(request.SaleUnit) ?? Clean(request.Unit) ?? "unidad",
            UnitsPerPackage = unitsPerPackage <= 0 ? 1 : unitsPerPackage,
            EntryDate = request.EntryDate.HasValue ? DateOnly.FromDateTime(request.EntryDate.Value) : DateOnly.FromDateTime(DateTime.UtcNow),
            SupplierId = request.SupplierId,
            Supplier = Clean(request.Supplier),
            PurchasePrice = request.PurchasePrice,
            SalePrice = request.SalePrice,
            WholesalePrice = request.WholesalePrice,
            Stock = ToLegacyStock(stockBase),
            StockBase = stockBase,
            MinimumStock = ToLegacyStock(minimumStockBase),
            MinimumStockBase = minimumStockBase,
            AverageCostBase = averageCostBase,
            ExpirationDate = request.ExpirationDate?.Date,
            Location = Clean(request.Location),
            Notes = Clean(request.Notes),
            IsActive = true
        };

        ApplyPresentations(product, request.Presentations, request);

        if (stockBase > 0)
        {
            product.InventoryMovementLogs.Add(new InventoryMovementLog
            {
                Reason = InventoryMovementReason.InitialStock,
                QuantityInput = stockBase,
                InputUnit = product.BaseUnit,
                QuantityBase = stockBase,
                UnitCostBase = averageCostBase,
                TotalCost = stockBase * averageCostBase,
                Notes = "Stock inicial"
            });
        }

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, ToResponse(product));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .Include(x => x.Presentations.Where(p => p.IsActive))
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return product is null ? NotFound() : Ok(ToResponse(product));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Update(Guid id, ProductRequest request, CancellationToken cancellationToken)
    {
        if (!ValidateProductRequest(request, out var error))
        {
            return BadRequest(error);
        }

        if (!ApiEnums.TryParseProductTrackingType(request.TrackingType, out var trackingType))
        {
            return BadRequest("Tipo de control de producto invalido.");
        }

        var product = await db.Products
            .Include(x => x.Presentations)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        var stockBase = request.StockBase ?? request.Stock;
        var minimumStockBase = request.MinimumStockBase ?? request.MinimumStock;
        var unitsPerPackage = request.UnitsPerPackage.GetValueOrDefault(product.UnitsPerPackage <= 0 ? 1 : product.UnitsPerPackage);

        product.Name = request.Name.Trim();
        product.Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        product.InternalCode = Clean(request.InternalCode);
        product.Barcode = Clean(request.Barcode);
        product.Brand = Clean(request.Brand);
        product.Presentation = Clean(request.Presentation);
        product.Unit = Clean(request.Unit);
        product.BaseUnit = Clean(request.BaseUnit) ?? Clean(request.Unit) ?? product.BaseUnit;
        product.TrackingType = trackingType;
        product.ProfitMarginPercent = request.ProfitMarginPercent ?? CalculateMargin(request.PurchasePrice, request.SalePrice);
        product.SuggestedPrice = request.SuggestedPrice;
        product.PurchaseUnit = Clean(request.PurchaseUnit) ?? Clean(request.Presentation) ?? Clean(request.Unit) ?? product.PurchaseUnit;
        product.SaleUnit = Clean(request.SaleUnit) ?? Clean(request.Unit) ?? product.SaleUnit;
        product.UnitsPerPackage = unitsPerPackage <= 0 ? 1 : unitsPerPackage;
        product.EntryDate = request.EntryDate.HasValue ? DateOnly.FromDateTime(request.EntryDate.Value) : product.EntryDate;
        product.SupplierId = request.SupplierId;
        product.Supplier = Clean(request.Supplier);
        product.PurchasePrice = request.PurchasePrice;
        product.SalePrice = request.SalePrice;
        product.WholesalePrice = request.WholesalePrice;
        product.Stock = ToLegacyStock(stockBase);
        product.StockBase = stockBase;
        product.MinimumStock = ToLegacyStock(minimumStockBase);
        product.MinimumStockBase = minimumStockBase;
        product.AverageCostBase = request.AverageCostBase ?? product.AverageCostBase;
        product.ExpirationDate = request.ExpirationDate?.Date;
        product.Location = Clean(request.Location);
        product.Notes = Clean(request.Notes);
        product.UpdatedAt = DateTime.UtcNow;

        ApplyPresentations(product, request.Presentations, request);

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(product));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void ApplyPresentations(Product product, IReadOnlyList<ProductPresentationRequest>? presentations, ProductRequest request)
    {
        foreach (var existing in product.Presentations)
        {
            existing.IsActive = false;
        }

        var requested = presentations is { Count: > 0 }
            ? presentations
            : [BuildDefaultPresentationRequest(request, product)];

        foreach (var item in requested)
        {
            var quantityInBase = item.QuantityInBaseUnit <= 0 ? 1 : item.QuantityInBaseUnit;
            product.Presentations.Add(new ProductPresentation
            {
                Name = string.IsNullOrWhiteSpace(item.Name) ? product.SaleUnit ?? product.BaseUnit : item.Name.Trim(),
                UnitLabel = Clean(item.UnitLabel) ?? product.BaseUnit,
                QuantityInBaseUnit = quantityInBase,
                PurchaseEnabled = item.PurchaseEnabled,
                SaleEnabled = item.SaleEnabled,
                Barcode = Clean(item.Barcode),
                PurchaseCost = item.PurchaseCost,
                SalePrice = item.SalePrice,
                WholesalePrice = item.WholesalePrice,
                SuggestedPrice = item.SuggestedPrice,
                ProfitMarginPercent = item.ProfitMarginPercent,
                IsDefaultPurchase = item.IsDefaultPurchase,
                IsDefaultSale = item.IsDefaultSale,
                IsActive = true
            });
        }
    }

    private static ProductPresentationRequest BuildDefaultPresentationRequest(ProductRequest request, Product product) => new(
        null,
        Clean(request.SaleUnit) ?? Clean(request.Unit) ?? "Unidad",
        Clean(request.SaleUnit) ?? Clean(request.Unit) ?? "unidad",
        1,
        true,
        true,
        request.Barcode,
        request.PurchasePrice,
        request.SalePrice,
        request.WholesalePrice,
        request.SuggestedPrice,
        product.ProfitMarginPercent,
        true,
        true);

    private static bool ValidateProductRequest(ProductRequest request, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            error = "El nombre del producto es obligatorio.";
            return false;
        }

        if (request.SalePrice < 0 || request.PurchasePrice < 0 || request.WholesalePrice.GetValueOrDefault() < 0 || request.Stock < 0 || request.MinimumStock < 0)
        {
            error = "Precios y stock no pueden ser negativos.";
            return false;
        }

        if (request.StockBase.GetValueOrDefault() < 0 ||
            request.MinimumStockBase.GetValueOrDefault() < 0 ||
            request.AverageCostBase.GetValueOrDefault() < 0 ||
            (request.UnitsPerPackage.HasValue && request.UnitsPerPackage.Value <= 0))
        {
            error = "Las cantidades avanzadas no pueden ser negativas.";
            return false;
        }

        return true;
    }

    private static ProductResponse ToResponse(Product product) => new(
        product.Id,
        product.Name,
        product.Category,
        product.InternalCode,
        product.Barcode,
        product.Brand,
        product.Presentation,
        product.Unit,
        product.Supplier,
        product.PurchasePrice,
        product.SalePrice,
        product.WholesalePrice,
        product.Stock,
        product.MinimumStock,
        product.ExpirationDate,
        product.Location,
        product.Notes,
        product.IsActive,
        IsLowStock(product),
        product.BaseUnit,
        product.TrackingType.ToApiValue(),
        product.ProfitMarginPercent,
        product.SuggestedPrice,
        product.PurchaseUnit,
        product.SaleUnit,
        product.UnitsPerPackage,
        product.EntryDate.HasValue ? product.EntryDate.Value.ToDateTime(TimeOnly.MinValue) : null,
        product.SupplierId,
        product.StockBase,
        product.MinimumStockBase,
        product.AverageCostBase,
        product.Presentations.Where(x => x.IsActive).OrderByDescending(x => x.IsDefaultSale).Select(ToPresentationResponse).ToList());

    private static ProductPresentationResponse ToPresentationResponse(ProductPresentation presentation) => new(
        presentation.Id,
        presentation.Name,
        presentation.UnitLabel,
        presentation.QuantityInBaseUnit,
        presentation.PurchaseEnabled,
        presentation.SaleEnabled,
        presentation.Barcode,
        presentation.PurchaseCost,
        presentation.SalePrice,
        presentation.WholesalePrice,
        presentation.SuggestedPrice,
        presentation.ProfitMarginPercent,
        presentation.IsDefaultPurchase,
        presentation.IsDefaultSale,
        presentation.IsActive);

    private static bool IsLowStock(Product product) =>
        (product.StockBase > 0 || product.MinimumStockBase > 0)
            ? product.StockBase <= product.MinimumStockBase
            : product.Stock <= product.MinimumStock;

    private static decimal? CalculateMargin(decimal cost, decimal price) =>
        cost <= 0 ? null : Math.Round(((price - cost) / cost) * 100, 2);

    private static int ToLegacyStock(decimal stockBase) => stockBase <= 0 ? 0 : (int)Math.Floor(stockBase);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
