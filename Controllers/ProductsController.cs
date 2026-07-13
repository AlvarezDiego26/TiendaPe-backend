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
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var term = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim().ToLowerInvariant()}%";

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = term is null && !onlyLowStock
            ? """
                select id, name, category, internal_code, barcode, brand, presentation, unit, supplier,
                       purchase_price, sale_price, wholesale_price, stock, minimum_stock, expiration_date,
                       location, notes, is_active
                from products
                where is_active = true
                limit @limit;
                """
            : """
                select id, name, category, internal_code, barcode, brand, presentation, unit, supplier,
                       purchase_price, sale_price, wholesale_price, stock, minimum_stock, expiration_date,
                       location, notes, is_active
                from products
                where is_active = true
                  and (
                    @search is null
                    or lower(name) like @search
                    or lower(category) like @search
                    or lower(coalesce(internal_code, '')) like @search
                    or lower(coalesce(barcode, '')) like @search
                    or lower(coalesce(brand, '')) like @search
                  )
                  and (@only_low_stock = false or stock <= minimum_stock)
                limit @limit;
                """;
        AddParameter(command, "search", term, System.Data.DbType.String);
        AddParameter(command, "only_low_stock", onlyLowStock, System.Data.DbType.Boolean);
        AddParameter(command, "limit", Math.Clamp(limit, 1, 1000), System.Data.DbType.Int32);

        var products = new List<ProductResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(ToResponse(reader));
        }

        return Ok(products);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(ProductRequest request, CancellationToken cancellationToken)
    {
        if (request.SalePrice < 0 || request.PurchasePrice < 0 || request.WholesalePrice < 0 || request.Stock < 0 || request.MinimumStock < 0)
        {
            return BadRequest("Precios y stock no pueden ser negativos.");
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            InternalCode = Clean(request.InternalCode),
            Barcode = Clean(request.Barcode),
            Brand = Clean(request.Brand),
            Presentation = Clean(request.Presentation),
            Unit = Clean(request.Unit),
            Supplier = Clean(request.Supplier),
            PurchasePrice = request.PurchasePrice,
            SalePrice = request.SalePrice,
            WholesalePrice = request.WholesalePrice,
            Stock = request.Stock,
            MinimumStock = request.MinimumStock,
            ExpirationDate = request.ExpirationDate?.Date,
            Location = Clean(request.Location),
            Notes = Clean(request.Notes),
            IsActive = true
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, ToResponse(product));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = """
            select id, name, category, internal_code, barcode, brand, presentation, unit, supplier,
                   purchase_price, sale_price, wholesale_price, stock, minimum_stock, expiration_date,
                   location, notes, is_active
            from products
            where id = @id
            limit 1;
            """;
        AddParameter(command, "id", id, System.Data.DbType.Guid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Ok(ToResponse(reader)) : NotFound();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Update(Guid id, ProductRequest request, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        product.Name = request.Name.Trim();
        product.Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        product.InternalCode = Clean(request.InternalCode);
        product.Barcode = Clean(request.Barcode);
        product.Brand = Clean(request.Brand);
        product.Presentation = Clean(request.Presentation);
        product.Unit = Clean(request.Unit);
        product.Supplier = Clean(request.Supplier);
        product.PurchasePrice = request.PurchasePrice;
        product.SalePrice = request.SalePrice;
        product.WholesalePrice = request.WholesalePrice;
        product.Stock = request.Stock;
        product.MinimumStock = request.MinimumStock;
        product.ExpirationDate = request.ExpirationDate?.Date;
        product.Location = Clean(request.Location);
        product.Notes = Clean(request.Notes);

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
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
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
        product.Stock <= product.MinimumStock);

    private static ProductResponse ToResponse(System.Data.Common.DbDataReader reader)
    {
        var stock = reader.GetInt32(12);
        var minimumStock = reader.GetInt32(13);

        return new ProductResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetDecimal(9),
            reader.GetDecimal(10),
            reader.IsDBNull(11) ? null : reader.GetDecimal(11),
            stock,
            minimumStock,
            reader.IsDBNull(14) ? null : reader.GetDateTime(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetString(16),
            reader.GetBoolean(17),
            stock <= minimumStock);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
