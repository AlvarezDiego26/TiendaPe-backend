using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaPe.Api;
using TiendaPe.Domain.Entities;
using TiendaPe.Infrastructure.Persistence;

namespace TiendaPe.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(TiendaPeDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<SummaryResponse>> Summary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var salesQuery = db.Sales.AsNoTracking();
        var expensesQuery = db.Expenses.AsNoTracking();
        var saleItemsQuery = db.SaleItems.AsNoTracking().Include(x => x.Product).Where(x => true);

        if (from.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.OccurredAt >= from.Value);
            expensesQuery = expensesQuery.Where(x => x.OccurredAt >= from.Value);
            saleItemsQuery = saleItemsQuery.Where(x => x.Sale.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.OccurredAt <= to.Value);
            expensesQuery = expensesQuery.Where(x => x.OccurredAt <= to.Value);
            saleItemsQuery = saleItemsQuery.Where(x => x.Sale.OccurredAt <= to.Value);
        }

        var income = await salesQuery.SumAsync(x => x.Total, cancellationToken);
        var expenses = await expensesQuery.SumAsync(x => x.Amount, cancellationToken);
        var costOfGoodsSold = await saleItemsQuery.SumAsync(
            x => (x.UnitCostBase ?? x.Product.PurchasePrice) * (x.QuantityBase == 0 ? x.Quantity : x.QuantityBase),
            cancellationToken);
        var cashSales = await salesQuery.Where(x => x.PaymentMethod == PaymentMethod.Cash).SumAsync(x => x.Total, cancellationToken);
        var digitalSales = await salesQuery.Where(x => x.PaymentMethod == PaymentMethod.YapePlin).SumAsync(x => x.Total, cancellationToken);

        return Ok(new SummaryResponse(
            income,
            expenses,
            costOfGoodsSold,
            income - costOfGoodsSold - expenses,
            cashSales,
            digitalSales));
    }

    [HttpGet("top-products")]
    public async Task<ActionResult<IReadOnlyList<TopProductResponse>>> TopProducts(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = db.SaleItems.AsNoTracking().AsQueryable();

            if (from.HasValue)
            {
                query = query.Where(x => x.Sale.OccurredAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(x => x.Sale.OccurredAt <= to.Value);
            }

            var result = await query
                .GroupBy(x => new { x.ProductId, x.ProductName })
                .Select(x => new TopProductResponse(
                    x.Key.ProductId,
                    x.Key.ProductName,
                    x.Sum(i => i.Quantity),
                    x.Sum(i => i.Subtotal),
                    x.Sum(i => i.QuantityBase == 0 ? i.Quantity : i.QuantityBase)))
                .OrderByDescending(x => x.QuantityBase == 0 ? x.Quantity : x.QuantityBase)
                .Take(Math.Clamp(limit, 1, 50))
                .ToListAsync(cancellationToken);

            return Ok(result);
        }
        catch (Exception ex) when (ex is TimeoutException || ex.InnerException is TimeoutException)
        {
            return Ok(Array.Empty<TopProductResponse>());
        }
    }

    [HttpGet("stagnant-products")]
    public async Task<ActionResult<IReadOnlyList<StagnantProductResponse>>> StagnantProducts(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365));

        var recentProductIds = await db.SaleItems
            .AsNoTracking()
            .Where(x => x.Sale.OccurredAt >= since)
            .Select(x => x.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var lastSales = await db.SaleItems
            .AsNoTracking()
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, LastSaleAt = x.Max(i => i.Sale.OccurredAt) })
            .ToDictionaryAsync(x => x.ProductId, x => (DateTime?)x.LastSaleAt, cancellationToken);

        var products = await db.Products
            .AsNoTracking()
            .Where(x => x.IsActive && !recentProductIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(products.Select(x => new StagnantProductResponse(
            x.Id,
            x.Name,
            x.Stock,
            lastSales.GetValueOrDefault(x.Id))).ToList());
    }

    [HttpGet("inventory-value")]
    public async Task<ActionResult<InventoryValueResponse>> InventoryValue(CancellationToken cancellationToken)
    {
        var value = await db.Products
            .AsNoTracking()
            .Where(x => x.IsActive)
            .SumAsync(x => (x.StockBase == 0 ? x.Stock : x.StockBase) * (x.AverageCostBase == 0 ? x.PurchasePrice : x.AverageCostBase), cancellationToken);

        return Ok(new InventoryValueResponse(value));
    }
}
