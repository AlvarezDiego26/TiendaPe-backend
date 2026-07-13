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
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = """
            select
              (select coalesce(sum(total), 0) from sales where (@from is null or occurred_at >= @from) and (@to is null or occurred_at <= @to)) as income,
              (select coalesce(sum(amount), 0) from expenses where (@from is null or occurred_at >= @from) and (@to is null or occurred_at <= @to)) as expenses,
              (select coalesce(sum(si.quantity * p.purchase_price), 0)
                 from sale_items si
                 join sales s on s.id = si.sale_id
                 join products p on p.id = si.product_id
                where (@from is null or s.occurred_at >= @from)
                  and (@to is null or s.occurred_at <= @to)) as cost_of_goods_sold,
              (select coalesce(sum(total), 0) from sales where payment_method = 'cash'::payment_method and (@from is null or occurred_at >= @from) and (@to is null or occurred_at <= @to)) as cash_sales,
              (select coalesce(sum(total), 0) from sales where payment_method = 'yape_plin'::payment_method and (@from is null or occurred_at >= @from) and (@to is null or occurred_at <= @to)) as digital_sales;
            """;

        var fromParameter = command.CreateParameter();
        fromParameter.ParameterName = "from";
        fromParameter.DbType = System.Data.DbType.DateTime;
        fromParameter.Value = from.HasValue ? from.Value : DBNull.Value;
        command.Parameters.Add(fromParameter);

        var toParameter = command.CreateParameter();
        toParameter.ParameterName = "to";
        toParameter.DbType = System.Data.DbType.DateTime;
        toParameter.Value = to.HasValue ? to.Value : DBNull.Value;
        command.Parameters.Add(toParameter);

        decimal income;
        decimal expenses;
        decimal costOfGoodsSold;
        decimal cashSales;
        decimal digitalSales;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            income = reader.GetDecimal(0);
            expenses = reader.GetDecimal(1);
            costOfGoodsSold = reader.GetDecimal(2);
            cashSales = reader.GetDecimal(3);
            digitalSales = reader.GetDecimal(4);
        }

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
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 20;
        command.CommandText = from.HasValue || to.HasValue
            ? """
                select
                    si.product_id,
                    si.product_name,
                    coalesce(sum(si.quantity), 0)::int as quantity,
                    coalesce(sum(si.subtotal), 0) as income
                from sale_items si
                join sales s on s.id = si.sale_id
                where (@from is null or s.occurred_at >= @from)
                  and (@to is null or s.occurred_at <= @to)
                group by si.product_id, si.product_name
                order by quantity desc
                limit @limit;
                """
            : """
                select
                    product_id,
                    product_name,
                    coalesce(sum(quantity), 0)::int as quantity,
                    coalesce(sum(subtotal), 0) as income
                from sale_items
                group by product_id, product_name
                order by quantity desc
                limit @limit;
                """;

        AddParameter(command, "from", from, System.Data.DbType.DateTime);
        AddParameter(command, "to", to, System.Data.DbType.DateTime);
        AddParameter(command, "limit", Math.Clamp(limit, 1, 50), System.Data.DbType.Int32);

        try
        {
            var result = new List<TopProductResponse>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new TopProductResponse(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetDecimal(3)));
            }

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
            .SumAsync(x => x.Stock * x.PurchasePrice, cancellationToken);

        return Ok(new InventoryValueResponse(value));
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object? value,
        System.Data.DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
