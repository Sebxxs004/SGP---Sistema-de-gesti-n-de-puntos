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
            .Where(p => p.Category != null && p.Category.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.SKU,
                p.BasePrice,
                p.TaxRate,
                p.MinStockLevel,
                p.CategoryId,
                p.IsComposite,
                p.IsActive,
                Category = p.Category!.Name
            })
            .ToListAsync(cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();
        var componentsByCompositeId = await _dbContext.ProductComponents
            .AsNoTracking()
            .Where(pc => productIds.Contains(pc.CompositeProductId))
            .Select(pc => new ProductCompositeComponentDto(
                pc.CompositeProductId,
                pc.ComponentId,
                pc.Component != null ? pc.Component.Name : "Componente",
                pc.Quantity
            ))
            .ToListAsync(cancellationToken);

        var groupedComponents = componentsByCompositeId
            .GroupBy(pc => pc.CompositeProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(item => new ProductComponentListItemDto(
                    item.ComponentId,
                    item.ComponentName,
                    item.Quantity
                )).ToList());

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
                categoryId = p.CategoryId,
                category = p.Category,
                price = p.BasePrice,
                taxRate = p.TaxRate,
                minStockLevel = p.MinStockLevel,
                isComposite = p.IsComposite,
                components = groupedComponents.TryGetValue(p.Id, out var components) ? components : new List<ProductComponentListItemDto>(),
                isActive = p.IsActive,
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

    [HttpGet("alerts/low-stock")]
    public async Task<IActionResult> GetLowStockAlerts(CancellationToken cancellationToken)
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
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar alertas de inventario." }
            });
        }

        var branchId = _currentUser.BranchId.Value;

        var data = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsComposite)
            .GroupJoin(
                _dbContext.BranchStocks.AsNoTracking().Where(bs => bs.BranchId == branchId),
                p => p.Id,
                bs => bs.ProductId,
                (p, stocks) => new
                {
                    Product = p,
                    CurrentStock = stocks.Select(s => (decimal?)s.Quantity).FirstOrDefault() ?? 0m
                })
            .Where(x => x.CurrentStock <= x.Product.MinStockLevel)
            .OrderBy(x => x.CurrentStock)
            .ThenBy(x => x.Product.Name)
            .Select(x => new LowStockAlertDto(
                x.Product.Id,
                x.Product.Name,
                x.Product.SKU,
                x.CurrentStock,
                x.Product.MinStockLevel
            ))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                branchId,
                alerts = data
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
            .Where(bs => bs.BranchId == branchId && bs.Quantity > 0)
            .Where(bs => bs.Product != null && bs.Product.IsActive && bs.Product.Category != null && bs.Product.Category.IsActive)
            .Select(bs => new CatalogProductSyncDto(
                bs.ProductId,
                bs.Product!.Name,
                bs.Product.SKU,
                bs.Product.BasePrice,
                bs.Product.CategoryId,
                bs.Product.Category!.Name,
                bs.Product.TaxRate,
                bs.Quantity
            ))
            .Distinct()
            .ToListAsync(cancellationToken);

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
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
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
                error = new { code = "Tenant.Required", message = "Debes enviar X-Tenant-Id para crear productos." }
            });
        }

        if (_currentUser.BranchId == null || _currentUser.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para operar inventario." }
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Sku) || request.BasePrice <= 0 || request.CategoryId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.Invalid", message = "Nombre, SKU, Precio base y Categoria son obligatorios." }
            });
        }

        if (request.TaxRate < 0m || request.TaxRate > 100m)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.InvalidTaxRate", message = "La tarifa de impuesto debe estar entre 0 y 100." }
            });
        }

        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (!categoryExists)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Inventory.CategoryNotFound", message = "La categoria especificada no existe o está inactiva." }
            });
        }

        var normalizedSku = request.Sku.Trim();
        var skuExists = await _dbContext.Products
            .AnyAsync(p => p.SKU.ToLower() == normalizedSku.ToLower(), cancellationToken);

        if (skuExists)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Inventory.SKUExists", message = $"El SKU '{normalizedSku}' ya existe en el inventario." }
            });
        }

        var normalizedBarcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        if (normalizedBarcode != null)
        {
            var barcodeExists = await _dbContext.Products
                .AnyAsync(p => p.Barcode != null && p.Barcode.ToLower() == normalizedBarcode.ToLower(), cancellationToken);
            if (barcodeExists)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "Inventory.BarcodeExists", message = $"El codigo de barras '{normalizedBarcode}' ya existe." }
                });
            }
        }

        var normalizedComponents = request.Components?
            .Where(c => c.ComponentId != Guid.Empty && c.Quantity > 0m)
            .GroupBy(c => c.ComponentId)
            .Select(g => new ProductComponentRequest(g.Key, g.Sum(x => x.Quantity)))
            .ToList() ?? new List<ProductComponentRequest>();

        if (request.IsComposite)
        {
            if (normalizedComponents.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "Inventory.ComponentsRequired", message = "Un producto compuesto debe tener al menos un ingrediente." }
                });
            }

            var componentIds = normalizedComponents.Select(c => c.ComponentId).ToList();
            var componentsFound = await _dbContext.Products
                .AsNoTracking()
                .Where(p => componentIds.Contains(p.Id) && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            var missingComponent = componentIds.Except(componentsFound).FirstOrDefault();
            if (missingComponent != Guid.Empty)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "Inventory.ComponentNotFound", message = "Uno o más ingredientes no existen o están inactivos." }
                });
            }
        }

        var product = new Product(
            _currentUser.TenantId.Value,
            request.CategoryId,
            request.Name.Trim(),
            normalizedSku,
            normalizedBarcode,
            request.BasePrice,
            request.Cost ?? request.BasePrice,
            request.IsComposite ? 0m : (request.MinStockLevel ?? 5m),
            request.IsComposite,
            request.TaxRate);

        await _dbContext.Products.AddAsync(product, cancellationToken);

        if (request.IsComposite)
        {
            foreach (var component in normalizedComponents)
            {
                if (component.ComponentId == product.Id)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = new { code = "Inventory.ComponentInvalid", message = "Un producto no puede ser ingrediente de sí mismo." }
                    });
                }

                await _dbContext.ProductComponents.AddAsync(new ProductComponent(
                    _currentUser.TenantId.Value,
                    product.Id,
                    component.ComponentId,
                    component.Quantity), cancellationToken);
            }
        }

        if (!request.IsComposite && request.InitialStock.HasValue && request.InitialStock.Value > 0)
        {
            var branchId = _currentUser.BranchId.Value;

            await _dbContext.BranchStocks.AddAsync(new BranchStock(
                _currentUser.TenantId.Value,
                branchId,
                product.Id,
                request.InitialStock.Value,
                0m), cancellationToken);

            var userId = _currentUser.Id ?? Guid.Empty;
            await _dbContext.StockMovements.AddAsync(new StockMovement(
                _currentUser.TenantId.Value,
                branchId,
                product.Id,
                userId,
                MovementType.In,
                request.InitialStock.Value,
                "Stock inicial"), cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/v1/inventory/products/{product.Id}", new
        {
            success = true,
            data = new { id = product.Id }
        });
    }

    [HttpPut("products/{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Sku) || request.BasePrice <= 0 || request.CategoryId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.Invalid", message = "Nombre, SKU, Precio base y Categoria son obligatorios." }
            });
        }

        if (request.TaxRate < 0m || request.TaxRate > 100m)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.InvalidTaxRate", message = "La tarifa de impuesto debe estar entre 0 y 100." }
            });
        }

        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Inventory.ProductNotFound", message = "Producto no encontrado." }
            });
        }

        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (!categoryExists)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Inventory.CategoryNotFound", message = "La categoria especificada no existe o está inactiva." }
            });
        }

        var normalizedSku = request.Sku.Trim();
        var duplicateSku = await _dbContext.Products
            .AnyAsync(p => p.Id != id && p.SKU.ToLower() == normalizedSku.ToLower(), cancellationToken);

        if (duplicateSku)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Inventory.SKUExists", message = $"El SKU '{normalizedSku}' ya existe en el inventario." }
            });
        }

        var normalizedBarcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        if (normalizedBarcode != null)
        {
            var duplicateBarcode = await _dbContext.Products
                .AnyAsync(p => p.Id != id && p.Barcode != null && p.Barcode.ToLower() == normalizedBarcode.ToLower(), cancellationToken);

            if (duplicateBarcode)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "Inventory.BarcodeExists", message = $"El codigo de barras '{normalizedBarcode}' ya existe." }
                });
            }
        }

        var normalizedComponents = request.Components?
            .Where(c => c.ComponentId != Guid.Empty && c.Quantity > 0m)
            .GroupBy(c => c.ComponentId)
            .Select(g => new ProductComponentRequest(g.Key, g.Sum(x => x.Quantity)))
            .ToList() ?? new List<ProductComponentRequest>();

        if (request.IsComposite)
        {
            if (normalizedComponents.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "Inventory.ComponentsRequired", message = "Un producto compuesto debe tener al menos un ingrediente." }
                });
            }

            if (normalizedComponents.Any(c => c.ComponentId == id))
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "Inventory.ComponentInvalid", message = "Un producto no puede ser ingrediente de sí mismo." }
                });
            }

            var componentIds = normalizedComponents.Select(c => c.ComponentId).ToList();
            var componentsFound = await _dbContext.Products
                .AsNoTracking()
                .Where(p => componentIds.Contains(p.Id) && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            var missingComponent = componentIds.Except(componentsFound).FirstOrDefault();
            if (missingComponent != Guid.Empty)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "Inventory.ComponentNotFound", message = "Uno o más ingredientes no existen o están inactivos." }
                });
            }
        }

        product.Update(
            request.CategoryId,
            request.Name.Trim(),
            normalizedSku,
            normalizedBarcode,
            request.BasePrice,
            request.Cost ?? request.BasePrice,
            request.IsComposite ? 0m : (request.MinStockLevel ?? product.MinStockLevel),
            request.IsComposite,
            request.TaxRate);

        var existingComponents = await _dbContext.ProductComponents
            .Where(pc => pc.CompositeProductId == id)
            .ToListAsync(cancellationToken);

        if (existingComponents.Count > 0)
        {
            _dbContext.ProductComponents.RemoveRange(existingComponents);
        }

        if (request.IsComposite)
        {
            foreach (var component in normalizedComponents)
            {
                await _dbContext.ProductComponents.AddAsync(new ProductComponent(
                    product.TenantId,
                    id,
                    component.ComponentId,
                    component.Quantity), cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new { id = product.Id }
        });
    }

    [HttpDelete("products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Inventory.ProductNotFound", message = "Producto no encontrado." }
            });
        }

        var hasStockOrMovements = await _dbContext.BranchStocks.AnyAsync(bs => bs.ProductId == id && bs.Quantity > 0, cancellationToken)
            || await _dbContext.StockMovements.AnyAsync(sm => sm.ProductId == id, cancellationToken)
            || await _dbContext.ProductComponents.AnyAsync(pc => pc.CompositeProductId == id || pc.ComponentId == id, cancellationToken);

        if (hasStockOrMovements)
        {
            product.Deactivate();
        }
        else
        {
            _dbContext.Products.Remove(product);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                id,
                deleted = !hasStockOrMovements,
                deactivated = hasStockOrMovements
            }
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

    [HttpGet("reports/kardex")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetKardexReport([FromQuery] KardexReportRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        if (request.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "BranchId es obligatorio para generar el kardex." }
            });
        }

        if (request.ProductId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Product.Required", message = "ProductId es obligatorio para generar el kardex." }
            });
        }

        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Inventory.ProductNotFound", message = "Producto no encontrado." }
            });
        }

        var branch = await _coreDbContext.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);

        if (branch == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Branch.NotFound", message = "Sucursal no encontrada." }
            });
        }

        var openingBalanceQuery = _dbContext.StockMovements
            .AsNoTracking()
            .Where(sm => sm.BranchId == request.BranchId && sm.ProductId == request.ProductId);

        if (request.From.HasValue)
        {
            openingBalanceQuery = openingBalanceQuery.Where(sm => sm.CreatedAt < request.From.Value);
        }
        else
        {
            openingBalanceQuery = openingBalanceQuery.Where(sm => false);
        }

        var openingMovements = await openingBalanceQuery
            .OrderBy(sm => sm.CreatedAt)
            .ToListAsync(cancellationToken);

        var openingBalance = openingMovements.Sum(ResolveSignedQuantity);

        var movementsQuery = _dbContext.StockMovements
            .AsNoTracking()
            .Where(sm => sm.BranchId == request.BranchId && sm.ProductId == request.ProductId);

        if (request.From.HasValue)
        {
            movementsQuery = movementsQuery.Where(sm => sm.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            movementsQuery = movementsQuery.Where(sm => sm.CreatedAt <= request.To.Value);
        }

        var movements = await movementsQuery
            .OrderBy(sm => sm.CreatedAt)
            .Take(2000)
            .ToListAsync(cancellationToken);

        var runningBalance = openingBalance;
        var rows = movements.Select(sm =>
        {
            var signedQuantity = ResolveSignedQuantity(sm);
            runningBalance += signedQuantity;

            var entries = signedQuantity > 0 ? signedQuantity : 0m;
            var exits = signedQuantity < 0 ? Math.Abs(signedQuantity) : 0m;

            return new KardexRowDto(
                sm.Id,
                sm.CreatedAt,
                ResolveKardexMovementType(sm),
                signedQuantity,
                sm.Reference,
                entries,
                exits,
                runningBalance
            );
        }).ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                branchId = request.BranchId,
                branchName = branch.Name,
                productId = product.Id,
                productName = product.Name,
                from = request.From,
                to = request.To,
                openingBalance,
                rows
            }
        });
    }

    private static decimal ResolveSignedQuantity(StockMovement movement)
    {
        var absoluteQuantity = Math.Abs(movement.Quantity);

        return movement.MovementType switch
        {
            MovementType.Sale => -absoluteQuantity,
            MovementType.Out => -absoluteQuantity,
            MovementType.Adjustment => movement.Quantity,
            _ => movement.Quantity >= 0 ? absoluteQuantity : -absoluteQuantity
        };
    }

    private static string ResolveKardexMovementType(StockMovement movement)
    {
        if (movement.MovementType == MovementType.Sale)
        {
            return "Venta";
        }

        if (movement.MovementType == MovementType.Adjustment)
        {
            return "Ajuste";
        }

        var normalizedReference = movement.Reference?.Trim().ToLowerInvariant() ?? string.Empty;

        if (movement.MovementType == MovementType.In && normalizedReference.Contains("compra"))
        {
            return "Compra";
        }

        if (movement.MovementType == MovementType.In && normalizedReference.Contains("devol"))
        {
            return "Devolucion";
        }

        if (movement.MovementType == MovementType.Out)
        {
            return "Salida";
        }

        if (movement.MovementType == MovementType.Transfer)
        {
            return "Transferencia";
        }

        if (movement.MovementType == MovementType.In)
        {
            return "Entrada";
        }

        return movement.MovementType.ToString();
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
    string CategoryName,
    decimal TaxRate,
    decimal Stock
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

public sealed record KardexReportRequest(
    Guid BranchId,
    Guid ProductId,
    DateTimeOffset? From,
    DateTimeOffset? To
);

public sealed record KardexRowDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    string MovementType,
    decimal Quantity,
    string? Reference,
    decimal Entries,
    decimal Exits,
    decimal Balance
);

public sealed record UpsertCategoryRequest(
    string Name,
    string? Description
);

public sealed record CreateProductRequest(
    Guid CategoryId,
    string Name,
    string Sku,
    decimal BasePrice,
    decimal TaxRate,
    decimal? InitialStock,
    string? Barcode,
    decimal? Cost,
    decimal? MinStockLevel,
    bool IsComposite,
    List<ProductComponentRequest>? Components
);

public sealed record UpdateProductRequest(
    Guid CategoryId,
    string Name,
    string Sku,
    decimal BasePrice,
    decimal TaxRate,
    string? Barcode,
    decimal? Cost,
    decimal? MinStockLevel,
    bool IsComposite,
    List<ProductComponentRequest>? Components
);

public sealed record LowStockAlertDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal CurrentStock,
    decimal MinStockLevel
);

public sealed record ProductComponentRequest(
    Guid ComponentId,
    decimal Quantity
);

public sealed record ProductCompositeComponentDto(
    Guid CompositeProductId,
    Guid ComponentId,
    string ComponentName,
    decimal Quantity
);

public sealed record ProductComponentListItemDto(
    Guid ComponentId,
    string ComponentName,
    decimal Quantity
);

public sealed record CategoryListItemDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive
);
