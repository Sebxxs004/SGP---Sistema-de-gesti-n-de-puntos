using Gibag.Modules.Core.Infrastructure;
using Gibag.Modules.Inventory.Domain;
using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Inventory.Presentation.Controllers;

[ApiController]
[Route("api/v1/inventory")]
[Authorize(Roles = "Admin")]
public class PurchasesController : ControllerBase
{
    private readonly InventoryDbContext _dbContext;
    private readonly CoreDbContext _coreDbContext;
    private readonly ICurrentUser _currentUser;

    public PurchasesController(InventoryDbContext dbContext, CoreDbContext coreDbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
        _currentUser = currentUser;
    }

    [HttpGet("purchases")]
    public async Task<IActionResult> GetPurchases([FromQuery] PurchaseHistoryFilterRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var purchasesQuery = _dbContext.Purchases
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .AsQueryable();

        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            purchasesQuery = purchasesQuery.Where(p => p.BranchId == request.BranchId.Value);
        }

        if (request.SupplierId.HasValue && request.SupplierId.Value != Guid.Empty)
        {
            purchasesQuery = purchasesQuery.Where(p => p.SupplierId == request.SupplierId.Value);
        }

        if (request.From.HasValue)
        {
            purchasesQuery = purchasesQuery.Where(p => p.PurchaseDate >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            purchasesQuery = purchasesQuery.Where(p => p.PurchaseDate <= request.To.Value);
        }

        var purchases = await purchasesQuery
            .OrderByDescending(p => p.PurchaseDate)
            .Take(500)
            .ToListAsync(cancellationToken);

        var branchIds = purchases.Select(p => p.BranchId).Distinct().ToList();
        var branchNames = await _coreDbContext.Branches
            .AsNoTracking()
            .Where(b => branchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        var data = purchases.Select(purchase => new
        {
            purchaseId = purchase.Id,
            purchase.BranchId,
            branchName = branchNames.TryGetValue(purchase.BranchId, out var branchName) ? branchName : "Sucursal desconocida",
            purchase.SupplierId,
            supplierName = purchase.Supplier?.Name ?? "Proveedor desconocido",
            purchase.PurchaseDate,
            purchase.TotalAmount,
            purchase.ReferenceNumber,
            items = purchase.Items.Select(item => new
            {
                item.Id,
                item.ProductId,
                productName = item.Product?.Name ?? "Producto desconocido",
                item.Quantity,
                item.UnitCost,
                subtotal = item.Quantity * item.UnitCost
            })
        });

        return Ok(new
        {
            success = true,
            data
        });
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers(CancellationToken cancellationToken)
    {
        var suppliers = await _dbContext.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(
                s.Id,
                s.Name,
                s.ContactName,
                s.Phone,
                s.Email,
                s.Address
            ))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = suppliers
        });
    }

    [HttpPost("suppliers")]
    public async Task<IActionResult> CreateSupplier([FromBody] UpsertSupplierRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        if (_currentUser.TenantId == null || _currentUser.TenantId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Tenant.Required", message = "Debes enviar X-Tenant-Id para crear proveedores." }
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.Invalid", message = "El nombre del proveedor es obligatorio." }
            });
        }

        var normalizedName = request.Name.Trim();
        var duplicated = await _dbContext.Suppliers
            .AnyAsync(s => s.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (duplicated)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Supplier.Duplicate", message = "Ya existe un proveedor con este nombre." }
            });
        }

        var supplier = new Supplier(
            _currentUser.TenantId.Value,
            normalizedName,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address);

        await _dbContext.Suppliers.AddAsync(supplier, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new SupplierDto(
                supplier.Id,
                supplier.Name,
                supplier.ContactName,
                supplier.Phone,
                supplier.Email,
                supplier.Address
            )
        });
    }

    [HttpPut("suppliers/{id:guid}")]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] UpsertSupplierRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.Invalid", message = "El nombre del proveedor es obligatorio." }
            });
        }

        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Supplier.NotFound", message = "Proveedor no encontrado." }
            });
        }

        var normalizedName = request.Name.Trim();
        var duplicated = await _dbContext.Suppliers
            .AnyAsync(s => s.Id != id && s.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (duplicated)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Supplier.Duplicate", message = "Ya existe un proveedor con este nombre." }
            });
        }

        supplier.Update(
            normalizedName,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new SupplierDto(
                supplier.Id,
                supplier.Name,
                supplier.ContactName,
                supplier.Phone,
                supplier.Email,
                supplier.Address
            )
        });
    }

    [HttpDelete("suppliers/{id:guid}")]
    public async Task<IActionResult> DeleteSupplier(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Supplier.NotFound", message = "Proveedor no encontrado." }
            });
        }

        var hasPurchases = await _dbContext.Purchases.AnyAsync(p => p.SupplierId == id, cancellationToken);
        if (hasPurchases)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Supplier.HasPurchases", message = "No se puede eliminar un proveedor con compras registradas." }
            });
        }

        _dbContext.Suppliers.Remove(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new { id }
        });
    }

    [HttpPost("purchases")]
    public async Task<IActionResult> CreatePurchase([FromBody] CreatePurchaseRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        if (_currentUser.TenantId == null || _currentUser.TenantId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Tenant.Required", message = "Debes enviar X-Tenant-Id para registrar compras." }
            });
        }

        if (request.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "La sucursal destino es obligatoria." }
            });
        }

        if (request.SupplierId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Supplier.Required", message = "Debes seleccionar un proveedor." }
            });
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Purchase.ItemsRequired", message = "La compra debe incluir al menos un producto." }
            });
        }

        if (request.Items.Any(i => i.ProductId == Guid.Empty || i.Quantity <= 0m || i.UnitCost < 0m))
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Purchase.InvalidItems", message = "Cada item debe tener producto valido, cantidad mayor a cero y costo unitario no negativo." }
            });
        }

        var branchExists = await _coreDbContext.Branches
            .AsNoTracking()
            .AnyAsync(b => b.Id == request.BranchId, cancellationToken);

        if (!branchExists)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.NotFound", message = "La sucursal destino no existe en este tenant." }
            });
        }

        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);

        if (supplier == null)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Supplier.NotFound", message = "El proveedor seleccionado no existe." }
            });
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToListAsync(cancellationToken);

        if (products.Count != productIds.Count)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Purchase.ProductNotFound", message = "Uno o mas productos no existen o estan inactivos." }
            });
        }

        var purchaseDate = request.PurchaseDate ?? DateTimeOffset.UtcNow;
        var purchase = new Purchase(
            _currentUser.TenantId.Value,
            request.BranchId,
            request.SupplierId,
            purchaseDate,
            request.ReferenceNumber);

        var userId = _currentUser.Id ?? Guid.Empty;

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Purchases.AddAsync(purchase, cancellationToken);

        foreach (var item in request.Items)
        {
            var purchaseItem = new PurchaseItem(
                purchase.Id,
                item.ProductId,
                item.Quantity,
                item.UnitCost);

            purchase.AddItem(purchaseItem);
            await _dbContext.PurchaseItems.AddAsync(purchaseItem, cancellationToken);

            var branchStock = await _dbContext.BranchStocks
                .FirstOrDefaultAsync(bs => bs.BranchId == request.BranchId && bs.ProductId == item.ProductId, cancellationToken);

            if (branchStock == null)
            {
                branchStock = new BranchStock(
                    _currentUser.TenantId.Value,
                    request.BranchId,
                    item.ProductId,
                    0m,
                    0m);

                await _dbContext.BranchStocks.AddAsync(branchStock, cancellationToken);
            }

            var newQuantity = branchStock.Quantity + item.Quantity;
            _dbContext.Entry(branchStock).Property(x => x.Quantity).CurrentValue = newQuantity;

            await _dbContext.StockMovements.AddAsync(new StockMovement(
                _currentUser.TenantId.Value,
                request.BranchId,
                item.ProductId,
                userId,
                MovementType.In,
                item.Quantity,
                "Ingreso por Compra"), cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                purchaseId = purchase.Id,
                purchase.BranchId,
                purchase.SupplierId,
                purchase.PurchaseDate,
                purchase.TotalAmount,
                purchase.ReferenceNumber,
                items = purchase.Items.Select(i => new
                {
                    i.ProductId,
                    i.Quantity,
                    i.UnitCost
                })
            }
        });
    }

    private bool IsAdmin() => string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
}

public sealed record UpsertSupplierRequest(
    string Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address
);

public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address
);

public sealed record CreatePurchaseRequest(
    Guid BranchId,
    Guid SupplierId,
    DateTimeOffset? PurchaseDate,
    string? ReferenceNumber,
    List<CreatePurchaseItemRequest> Items
);

public sealed record CreatePurchaseItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost
);

public sealed record PurchaseHistoryFilterRequest(
    Guid? BranchId,
    Guid? SupplierId,
    DateTimeOffset? From,
    DateTimeOffset? To
);
