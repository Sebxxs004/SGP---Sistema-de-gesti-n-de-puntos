using Gibag.Modules.Inventory.Application.Products.CreateProduct;
using Gibag.Modules.Inventory.Application.Stock.AdjustStock;
using Gibag.Modules.Inventory.Infrastructure;
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

    public InventoryController(ISender sender, ICurrentUser currentUser, InventoryDbContext dbContext)
    {
        _sender = sender;
        _currentUser = currentUser;
        _dbContext = dbContext;
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
