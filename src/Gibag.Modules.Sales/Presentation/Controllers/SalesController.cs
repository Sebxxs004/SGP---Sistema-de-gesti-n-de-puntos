using Gibag.Modules.Sales.Application.Sales.CreateSale;
using Gibag.Modules.Sales.Application.Sessions.CloseCashDrawer;
using Gibag.Modules.Sales.Application.Sessions.OpenCashDrawer;
using Gibag.Modules.Sales.Domain;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Shared.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Gibag.Modules.Sales.Presentation.Controllers;

[ApiController]
[Route("api/v1/sales")]
public class SalesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;
    private readonly SalesDbContext _dbContext;
    private readonly InventoryDbContext _inventoryDbContext;
    private readonly CoreDbContext _coreDbContext;

    public SalesController(
        ISender sender,
        ICurrentUser currentUser,
        SalesDbContext dbContext,
        InventoryDbContext inventoryDbContext,
        CoreDbContext coreDbContext)
    {
        _sender = sender;
        _currentUser = currentUser;
        _dbContext = dbContext;
        _inventoryDbContext = inventoryDbContext;
        _coreDbContext = coreDbContext;
    }

    [HttpGet("{id:guid}/ticket-data")]
    public async Task<IActionResult> GetTicketData(Guid id, CancellationToken cancellationToken)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar comprobantes." }
            });
        }

        var saleData = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Id == id && s.BranchId == currentBranchId.Value)
            .Select(s => new
            {
                s.Id,
                s.TenantId,
                s.BranchId,
                s.UserId,
                s.CreatedAt,
                s.SubTotal,
                s.Tax,
                s.Total,
                Details = s.Details.Select(d => new
                {
                    d.ProductId,
                    d.Quantity,
                    d.UnitPrice,
                    d.SubTotal
                }).ToList(),
                Payments = s.Payments.Select(p => new
                {
                    Method = p.Method,
                    p.Amount
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (saleData == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Sale.NotFound", message = "No se encontró la venta solicitada para esta sucursal." }
            });
        }

        var productIds = saleData.Details.Select(d => d.ProductId).Distinct().ToList();
        var productNames = await _inventoryDbContext.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var tenant = await _coreDbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == saleData.TenantId)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.TaxId,
                t.ThankYouMessage,
                t.TaxPercentage,
                t.CurrencySymbol
            })
            .FirstOrDefaultAsync(cancellationToken);

        var branch = await _coreDbContext.Branches
            .AsNoTracking()
            .Where(b => b.Id == saleData.BranchId)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.Address,
                b.Phone
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                saleId = saleData.Id,
                ticketNumber = saleData.Id.ToString()[..8].ToUpperInvariant(),
                issuedAt = saleData.CreatedAt,
                company = new
                {
                    id = tenant?.Id ?? saleData.TenantId,
                    name = tenant?.Name ?? "SGP",
                    taxId = tenant?.TaxId ?? "N/A",
                    thankYouMessage = tenant?.ThankYouMessage ?? "Gracias por su compra",
                    taxPercentage = tenant?.TaxPercentage ?? 16m,
                    currencySymbol = string.IsNullOrWhiteSpace(tenant?.CurrencySymbol) ? "$" : tenant?.CurrencySymbol
                },
                branch = new
                {
                    id = branch?.Id ?? saleData.BranchId,
                    name = branch?.Name ?? "Sucursal",
                    address = branch?.Address ?? "",
                    phone = branch?.Phone ?? ""
                },
                cashier = new
                {
                    id = saleData.UserId,
                    email = _currentUser.Email ?? "cajero@sgp.local"
                },
                items = saleData.Details.Select(d => new
                {
                    productId = d.ProductId,
                    productName = productNames.TryGetValue(d.ProductId, out var name)
                        ? name
                        : $"Producto {d.ProductId.ToString()[..8]}",
                    quantity = d.Quantity,
                    unitPrice = d.UnitPrice,
                    subTotal = d.SubTotal
                }),
                payments = saleData.Payments.Select(p => new
                {
                    method = p.Method.ToString(),
                    amount = p.Amount
                }),
                subTotal = saleData.SubTotal,
                tax = saleData.Tax,
                total = saleData.Total
            }
        });
    }

    [HttpGet("stats/summary")]
    public async Task<IActionResult> GetSummaryStats(CancellationToken cancellationToken)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar métricas." }
            });
        }

        var branchId = currentBranchId.Value;
        var utcNow = DateTimeOffset.UtcNow;
        var dayStart = new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var salesTodayQuery = _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.BranchId == branchId && s.CreatedAt >= dayStart && s.CreatedAt < dayEnd);

        var totalSalesToday = await salesTodayQuery.SumAsync(s => (decimal?)s.Total, cancellationToken) ?? 0m;
        var ticketsToday = await salesTodayQuery.CountAsync(cancellationToken);

        var topProductsRaw = await _dbContext.SaleDetails
            .AsNoTracking()
            .Where(d => d.Sale != null && d.Sale.BranchId == branchId && d.Sale.CreatedAt >= dayStart && d.Sale.CreatedAt < dayEnd)
            .GroupBy(d => d.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                Amount = g.Sum(x => x.SubTotal)
            })
            .OrderByDescending(x => x.Quantity)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topProductIds = topProductsRaw.Select(x => x.ProductId).ToList();
        var productNames = await _inventoryDbContext.Products
            .AsNoTracking()
            .Where(p => topProductIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var topProducts = topProductsRaw.Select(item => new
        {
            productId = item.ProductId,
            productName = productNames.TryGetValue(item.ProductId, out var name) ? name : $"Producto {item.ProductId.ToString()[..8]}",
            quantity = item.Quantity,
            amount = item.Amount
        });

        var paymentTodayRaw = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.Sale != null && p.Sale.BranchId == branchId && p.Sale.CreatedAt >= dayStart && p.Sale.CreatedAt < dayEnd)
            .GroupBy(p => p.Method)
            .Select(g => new
            {
                Method = g.Key,
                Amount = g.Sum(x => x.Amount)
            })
            .ToListAsync(cancellationToken);

        var cashAmount = paymentTodayRaw
            .Where(x => x.Method == PaymentMethod.Cash)
            .Sum(x => x.Amount);

        var cardAmount = paymentTodayRaw
            .Where(x => x.Method == PaymentMethod.CreditCard || x.Method == PaymentMethod.DebitCard)
            .Sum(x => x.Amount);

        var totalPaymentAmount = cashAmount + cardAmount;
        var cashPercentage = totalPaymentAmount == 0 ? 0 : Math.Round((cashAmount / totalPaymentAmount) * 100m, 2);
        var cardPercentage = totalPaymentAmount == 0 ? 0 : Math.Round((cardAmount / totalPaymentAmount) * 100m, 2);

        var weekStart = dayStart.AddDays(-6);
        var weeklySalesRows = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.BranchId == branchId && s.CreatedAt >= weekStart && s.CreatedAt < dayEnd)
            .Select(s => new { s.CreatedAt, s.Total })
            .ToListAsync(cancellationToken);

        var weeklyRaw = weeklySalesRows
            .GroupBy(s => s.CreatedAt.UtcDateTime.Date)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Sum(x => x.Total),
                Tickets = g.Count()
            })
            .ToList();

        var weekStartDate = dayStart.UtcDateTime.Date.AddDays(-6);

        var weeklySeries = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = weekStartDate.AddDays(offset);
                var found = weeklyRaw.FirstOrDefault(x => x.Date == date);
                return new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    total = found?.Total ?? 0m,
                    tickets = found?.Tickets ?? 0
                };
            })
            .ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                totalSalesToday,
                ticketsToday,
                topProducts,
                paymentDistribution = new
                {
                    cash = new { amount = cashAmount, percentage = cashPercentage },
                    card = new { amount = cardAmount, percentage = cardPercentage }
                },
                weeklySales = weeklySeries
            }
        });
    }

    [HttpGet("history/current-session")]
    public async Task<IActionResult> GetCurrentSessionSalesHistory(CancellationToken cancellationToken)
    {
        var currentBranchId = _currentUser.BranchId;
        var currentUserId = _currentUser.Id;

        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para ver historial de ventas." }
            });
        }

        if (currentUserId == null || currentUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                success = false,
                error = new { code = "Auth.UserMissing", message = "No se encontró usuario autenticado." }
            });
        }

        var activeSessionId = await _dbContext.CashRegisterSessions
            .AsNoTracking()
            .Where(s => s.UserId == currentUserId.Value && s.BranchId == currentBranchId.Value && s.IsOpen)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeSessionId == null)
        {
            return Ok(new
            {
                success = true,
                data = new
                {
                    sessionId = (Guid?)null,
                    sales = Array.Empty<object>()
                }
            });
        }

        var sales = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.SessionId == activeSessionId.Value)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                id = s.Id,
                createdAt = s.CreatedAt,
                subTotal = s.SubTotal,
                tax = s.Tax,
                total = s.Total,
                items = s.Details.Sum(d => d.Quantity),
                payments = s.Payments
                    .Select(p => new
                    {
                        method = p.Method.ToString(),
                        amount = p.Amount
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                sessionId = activeSessionId,
                sales
            }
        });
    }

    [HttpGet("sessions/active")]
    public async Task<IActionResult> GetActiveSession(CancellationToken cancellationToken)
    {
        var currentBranchId = _currentUser.BranchId;
        var currentUserId = _currentUser.Id;

        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar la sesión activa." }
            });
        }

        if (currentUserId == null || currentUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                success = false,
                error = new { code = "Auth.UserMissing", message = "No se encontró usuario autenticado." }
            });
        }

        var activeSession = await _dbContext.CashRegisterSessions
            .AsNoTracking()
            .Where(s => s.UserId == currentUserId.Value && s.BranchId == currentBranchId.Value && s.IsOpen)
            .Select(s => new
            {
                s.Id,
                s.BranchId,
                s.UserId,
                s.OpenedAt,
                s.InitialBalance,
                s.IsOpen
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = activeSession
        });
    }

    [HttpGet("sessions/history")]
    public async Task<IActionResult> GetSessionsHistory(CancellationToken cancellationToken)
    {
        var currentBranchId = _currentUser.BranchId;

        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar historial de cajas." }
            });
        }

        var sessions = await _dbContext.CashRegisterSessions
            .AsNoTracking()
            .Where(s => s.BranchId == currentBranchId.Value && !s.IsOpen && s.ClosedAt != null)
            .OrderByDescending(s => s.ClosedAt)
            .Select(s => new
            {
                s.Id,
                s.UserId,
                s.OpenedAt,
                s.ClosedAt,
                s.FinalBalanceExpected,
                s.FinalBalanceEncounted
            })
            .ToListAsync(cancellationToken);

        var userIds = sessions.Select(s => s.UserId).Distinct().ToList();

        var usersById = await _coreDbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(
                u => u.Id,
                u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}".Trim()) ? u.Email : $"{u.FirstName} {u.LastName}".Trim(),
                cancellationToken);

        var data = sessions.Select(s =>
        {
            var expected = s.FinalBalanceExpected ?? 0m;
            var counted = s.FinalBalanceEncounted ?? 0m;

            return new
            {
                id = s.Id,
                cashierName = usersById.TryGetValue(s.UserId, out var userName) ? userName : "Usuario desconocido",
                openedAt = s.OpenedAt,
                closedAt = s.ClosedAt,
                expectedAmount = expected,
                countedAmount = counted,
                difference = counted - expected
            };
        });

        return Ok(new
        {
            success = true,
            data
        });
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> OpenCashDrawer([FromBody] OpenCashDrawerCommand command)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para abrir caja." }
            });
        }

        var result = await _sender.Send(command with { BranchId = currentBranchId.Value });

        if (result.IsFailure)
        {
            return BadRequest(new { 
                success = false, 
                error = new { code = result.ErrorCode, message = result.ErrorMessage } 
            });
        }

        return Created($"/api/v1/sales/sessions/{result.Value}", new { 
            success = true, 
            data = new { id = result.Value } 
        });
    }

    [HttpPost("sessions/close")]
    public async Task<IActionResult> CloseCashDrawer([FromBody] CloseCashDrawerCommand command)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para cerrar caja." }
            });
        }

        var result = await _sender.Send(command with { BranchId = currentBranchId.Value });

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
            data = result.Value
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateSale([FromBody] CreateSaleCommand command)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para registrar ventas." }
            });
        }

        var result = await _sender.Send(command with { BranchId = currentBranchId.Value });

        if (result.IsFailure)
        {
            return BadRequest(new { 
                success = false, 
                error = new { code = result.ErrorCode, message = result.ErrorMessage } 
            });
        }

        // 201 Created or 200 OK since client might provide ID
        return Ok(new { 
            success = true, 
            data = new { id = result.Value } 
        });
    }
}
