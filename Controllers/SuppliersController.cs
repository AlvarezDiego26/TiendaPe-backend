using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaPe.Api;
using TiendaPe.Domain.Entities;
using TiendaPe.Infrastructure.Persistence;

namespace TiendaPe.Controllers;

[ApiController]
[Authorize]
[Route("api/suppliers")]
public sealed class SuppliersController(TiendaPeDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierResponse>>> Get(
        [FromQuery] string? search,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = db.Suppliers.AsNoTracking().Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                (x.Company != null && x.Company.ToLower().Contains(term)) ||
                (x.Phone != null && x.Phone.ToLower().Contains(term)));
        }

        var suppliers = await query
            .OrderBy(x => x.Name)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

        return Ok(suppliers.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return supplier is null ? NotFound() : Ok(ToResponse(supplier));
    }

    [HttpPost]
    public async Task<ActionResult<SupplierResponse>> Create(SupplierRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("El nombre del proveedor es obligatorio.");
        }

        var supplier = new Supplier
        {
            Name = request.Name.Trim(),
            Phone = Clean(request.Phone),
            Company = Clean(request.Company),
            Notes = Clean(request.Notes),
            IsActive = true
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, ToResponse(supplier));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SupplierResponse>> Update(Guid id, SupplierRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("El nombre del proveedor es obligatorio.");
        }

        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        supplier.Name = request.Name.Trim();
        supplier.Phone = Clean(request.Phone);
        supplier.Company = Clean(request.Company);
        supplier.Notes = Clean(request.Notes);
        supplier.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(supplier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        supplier.IsActive = false;
        supplier.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static SupplierResponse ToResponse(Supplier supplier) => new(
        supplier.Id,
        supplier.Name,
        supplier.Phone,
        supplier.Company,
        supplier.Notes,
        supplier.LastPurchaseAt,
        supplier.TotalPurchased,
        supplier.PendingBalance,
        supplier.IsActive);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
