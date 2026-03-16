using Gibag.Modules.Inventory.Application.Products.CreateProduct;
using Gibag.Modules.Inventory.Application.Stock.AdjustStock;
using Gibag.Modules.Inventory.Domain;
using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Shared.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Inventory.Presentation.Controllers;

[ApiController]
[Route("api/v1/inventory")]
// Uncomment this when Authorize middleware is fully working
// [Authorize] 
public class InventoryController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;
    private readonly InventoryDbContext _dbContext;
    private readonly CoreDbContext _coreDbContext;

    public InventoryController(ISender sender, ICurrentUser currentUser, InventoryDbContext dbContext, CoreDbContext coreDbContext)
    {
        _sender = sender;
        _currentUser = currentUser;
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
    }

    [HttpGet("stock")]
    public async Task<IActionResult> GetBranchStock(CancellationToken cancellationToken)
    {
        if (_currentUser.BranchId == null || _currentUser.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar inventario." }
            });
        }

        var branchId = _currentUser.BranchId.Value;

        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.SKU,
                p.BasePrice,
                Category = p.Category!.Name
            })
            .ToListAsync(cancellationToken);

        var stocks = await _dbContext.BranchStocks
            .AsNoTracking()
            .Where(bs => bs.BranchId == branchId)
            .ToDictionaryAsync(bs => bs.ProductId, bs => bs.Quantity, cancellationToken);

        var data = products
            .Select(p => new
            {
                p.Id,
                p.Name,
                sku = p.SKU,
                category = p.Category,
                price = p.BasePrice,
                stock = stocks.TryGetValue(p.Id, out var quantity) ? quantity : 0m
            })
            .OrderBy(p => p.Name)
            .ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                branchId,
                products = data
            }
        });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryListItemDto(
                c.Id,
                c.Name,
                c.Description,
                c.IsActive
            ))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = categories
        });
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] UpsertCategoryRequest request, CancellationToken cancellationToken)
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
                error = new { code = "Tenant.Required", message = "Debes enviar X-Tenant-Id para crear categorías." }
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.Invalid", message = "El nombre de la categoría es obligatorio." }
            });
        }

        var normalizedName = request.Name.Trim();
        var duplicated = await _dbContext.Categories
            .AnyAsync(c => c.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (duplicated)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Category.Duplicate", message = "Ya existe una categoría con este nombre." }
            });
        }

        var category = new Category(
            _currentUser.TenantId.Value,
            normalizedName,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim());

        await _dbContext.Categories.AddAsync(category, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new CategoryListItemDto(category.Id, category.Name, category.Description, category.IsActive)
        });
    }

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpsertCategoryRequest request, CancellationToken cancellationToken)
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
                error = new { code = "Validation.Invalid", message = "El nombre de la categoría es obligatorio." }
            });
        }

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Category.NotFound", message = "Categoría no encontrada." }
            });
        }

        var normalizedName = request.Name.Trim();
        var duplicated = await _dbContext.Categories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (duplicated)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Category.Duplicate", message = "Ya existe una categoría con este nombre." }
            });
        }

        category.Update(normalizedName, string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim());
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new CategoryListItemDto(category.Id, category.Name, category.Description, category.IsActive)
        });
    }

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Category.NotFound", message = "Categoría no encontrada." }
            });
        }

        var hasProducts = await _dbContext.Products.AnyAsync(p => p.CategoryId == id && p.IsActive, cancellationToken);
        if (hasProducts)
        {
            category.Deactivate();
        }
        else
        {
            _dbContext.Categories.Remove(category);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                id,
                deleted = !hasProducts,
                deactivated = hasProducts
            }
        });
    }

    [HttpGet("catalog/sync")]
    public async Task<IActionResult> SyncCatalog([FromQuery] DateTimeOffset? lastSyncDate, CancellationToken cancellationToken)
    {
        if (_currentUser.BranchId == null || _currentUser.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para sincronizar catálogo." }
            });
        }

        var branchId = _currentUser.BranchId.Value;

        var branchProducts = await _dbContext.BranchStocks
            .AsNoTracking()
            .Where(bs => bs.BranchId == branchId)
            .Select(bs => bs.Product!)
            .Where(p => p != null && p.IsActive && p.Category != null && p.Category.IsActive)
            .Select(p => new CatalogProductSyncDto(
                p!.Id,
                p.Name,
                p.SKU,
                p.BasePrice,
                p.CategoryId,
                p.Category!.Name
            ))
            .Distinct()
            .ToListAsync(cancellationToken);

        // Bootstrap fallback: if branch stock has not been configured yet,
        // expose active tenant products so POS can initialize its local catalog.
        if (branchProducts.Count == 0)
        {
            branchProducts = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.IsActive && p.Category != null && p.Category.IsActive)
                .Select(p => new CatalogProductSyncDto(
                    p.Id,
                    p.Name,
                    p.SKU,
                    p.BasePrice,
                    p.CategoryId,
                    p.Category!.Name
                ))
                .ToListAsync(cancellationToken);
        }

        var categories = branchProducts
            .GroupBy(p => new { p.CategoryId, p.CategoryName })
            .Select(g => new CatalogCategorySyncDto(g.Key.CategoryId, g.Key.CategoryName))
            .ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                branchId,
                syncedAtUtc = DateTimeOffset.UtcNow,
                lastSyncDate,
                categories,
                products = branchProducts
            }
        });
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductCommand command)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        if (_currentUser.BranchId == null || _currentUser.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para operar inventario." }
            });
        }

        var result = await _sender.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { 
                success = false, 
                error = new { code = result.ErrorCode, message = result.ErrorMessage } 
            });
        }

        return Created($"/api/v1/inventory/products/{result.Value}", new { 
            success = true, 
            data = new { id = result.Value } 
        });
    }

    [HttpPost("stock/adjust")]
    public async Task<IActionResult> AdjustStock([FromBody] AdjustStockRequest request)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        if (_currentUser.BranchId == null || _currentUser.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para ajustar inventario." }
            });
        }

        var branchId = request.BranchId == Guid.Empty ? _currentUser.BranchId.Value : request.BranchId;

        var result = await _sender.Send(new AdjustStockCommand(
            branchId,
            request.ProductId,
            request.QuantityDelta,
            request.Reason));

        if (result.IsFailure)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = result.ErrorCode, message = result.ErrorMessage }
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                movementId = result.Value,
                branchId,
                request.ProductId,
                request.QuantityDelta,
                request.Reason
            }
        });
    }

    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements([FromQuery] StockMovementsFilterRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.BranchId == null || _currentUser.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar movimientos." }
            });
        }

        var movementsQuery = _dbContext.StockMovements
            .AsNoTracking()
            .Include(sm => sm.Product)
            .AsQueryable();

        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            movementsQuery = movementsQuery.Where(sm => sm.BranchId == request.BranchId.Value);
        }

        if (request.ProductId.HasValue && request.ProductId.Value != Guid.Empty)
        {
            movementsQuery = movementsQuery.Where(sm => sm.ProductId == request.ProductId.Value);
        }

        if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
        {
            movementsQuery = movementsQuery.Where(sm => sm.UserId == request.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            var reason = request.Reason.Trim().ToLowerInvariant();
            movementsQuery = movementsQuery.Where(sm => sm.Reference != null && sm.Reference.ToLower().Contains(reason));
        }

        if (request.From.HasValue)
        {
            movementsQuery = movementsQuery.Where(sm => sm.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            movementsQuery = movementsQuery.Where(sm => sm.CreatedAt <= request.To.Value);
        }

        var movements = await movementsQuery
            .OrderByDescending(sm => sm.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var userIds = movements
            .Select(sm => sm.UserId)
            .Distinct()
            .ToList();

        var usersById = await _coreDbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(
                u => u.Id,
                u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}".Trim())
                    ? u.Email
                    : $"{u.FirstName} {u.LastName}".Trim(),
                cancellationToken);

        var data = movements.Select(sm => new StockMovementListItemDto(
            sm.Id,
            sm.BranchId,
            sm.ProductId,
            sm.Product?.Name ?? "Producto desconocido",
            sm.UserId,
            usersById.TryGetValue(sm.UserId, out var userName) ? userName : "Usuario desconocido",
            sm.MovementType.ToString(),
            sm.Quantity,
            sm.Reference,
            sm.CreatedAt
        )).ToList();

        return Ok(new
        {
            success = true,
            data
        });
    }

    private bool IsAdmin() => string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
}

public sealed record CatalogCategorySyncDto(Guid Id, string Name);

public sealed record CatalogProductSyncDto(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    Guid CategoryId,
    string CategoryName
);

public sealed record AdjustStockRequest(
    Guid BranchId,
    Guid ProductId,
    decimal QuantityDelta,
    string Reason
);

public sealed record StockMovementsFilterRequest(
    Guid? BranchId,
    Guid? ProductId,
    string? Reason,
    Guid? UserId,
    DateTimeOffset? From,
    DateTimeOffset? To
);

public sealed record StockMovementListItemDto(
    Guid Id,
    Guid BranchId,
    Guid ProductId,
    string ProductName,
    Guid UserId,
    string UserName,
    string MovementType,
    decimal Quantity,
    string? Reason,
    DateTimeOffset CreatedAt
);

public sealed record UpsertCategoryRequest(
    string Name,
    string? Description
);

public sealed record CategoryListItemDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive
);
