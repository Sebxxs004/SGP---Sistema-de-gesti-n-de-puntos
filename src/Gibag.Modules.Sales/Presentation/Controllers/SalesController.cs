using Gibag.Modules.Sales.Application.Sales.CreateSale;
using Gibag.Modules.Sales.Application.Sales.CompletePendingSale;
using Gibag.Modules.Sales.Application.Sales.RefundSale;
using Gibag.Modules.Sales.Application.Sessions.CloseCashDrawer;
using Gibag.Modules.Sales.Application.Sessions.OpenCashDrawer;
using Gibag.Modules.Sales.Application.Sessions.RegisterCashMovement;
using Gibag.Modules.Sales.Domain;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Shared.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
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
                s.CustomerId,
                s.CreatedAt,
                s.SubTotal,
                s.Tax,
                s.Total,
                s.Discount,
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

        var customer = saleData.CustomerId.HasValue
            ? await _coreDbContext.Customers
                .AsNoTracking()
                .Where(c => c.Id == saleData.CustomerId.Value)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.DocumentNumber
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var receivable = await _dbContext.AccountReceivables
            .AsNoTracking()
            .Where(ar => ar.SaleId == saleData.Id)
            .Select(ar => new
            {
                ar.Balance,
                ar.Status
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
                customer = customer == null
                    ? null
                    : new
                    {
                        id = customer.Id,
                        name = customer.Name,
                        documentNumber = customer.DocumentNumber
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
                isCreditSale = receivable != null,
                pendingBalance = receivable?.Balance ?? 0m,
                receivableStatus = receivable?.Status.ToString(),
                subTotal = saleData.SubTotal,
                discount = saleData.Discount,
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
            .Where(s => s.BranchId == branchId && s.Status != SaleStatus.Pending && s.CreatedAt >= dayStart && s.CreatedAt < dayEnd);

        var totalSalesToday = await salesTodayQuery.SumAsync(s => (decimal?)s.Total, cancellationToken) ?? 0m;
        var ticketsToday = await salesTodayQuery.CountAsync(cancellationToken);

        var topProductsRaw = await _dbContext.SaleDetails
            .AsNoTracking()
            .Where(d => d.Sale != null && d.Sale.BranchId == branchId && d.Sale.Status != SaleStatus.Pending && d.Sale.CreatedAt >= dayStart && d.Sale.CreatedAt < dayEnd)
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
            .Where(p => p.Sale != null && p.Sale.BranchId == branchId && p.Sale.Status != SaleStatus.Pending && p.Sale.CreatedAt >= dayStart && p.Sale.CreatedAt < dayEnd)
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

        var creditAmount = paymentTodayRaw
            .Where(x => x.Method == PaymentMethod.Credit)
            .Sum(x => x.Amount);

        var totalPaymentAmount = cashAmount + cardAmount + creditAmount;
        var cashPercentage = totalPaymentAmount == 0 ? 0 : Math.Round((cashAmount / totalPaymentAmount) * 100m, 2);
        var cardPercentage = totalPaymentAmount == 0 ? 0 : Math.Round((cardAmount / totalPaymentAmount) * 100m, 2);
        var creditPercentage = totalPaymentAmount == 0 ? 0 : Math.Round((creditAmount / totalPaymentAmount) * 100m, 2);

        var weekStart = dayStart.AddDays(-6);
        var weeklySalesRows = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.BranchId == branchId && s.Status != SaleStatus.Pending && s.CreatedAt >= weekStart && s.CreatedAt < dayEnd)
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
                    card = new { amount = cardAmount, percentage = cardPercentage },
                    credit = new { amount = creditAmount, percentage = creditPercentage }
                },
                weeklySales = weeklySeries
            }
        });
    }

    [HttpGet("reports/profitability")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetProfitabilityReport([FromQuery] ProfitabilityFilterRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar rentabilidad." }
            });
        }

        var utcNow = DateTimeOffset.UtcNow;
        var defaultFrom = new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, TimeSpan.Zero);
        var defaultTo = defaultFrom.AddDays(1);

        var from = request.From ?? defaultFrom;
        var to = request.To ?? defaultTo;

        var salesQuery = _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.BranchId == currentBranchId.Value
                && s.Status == SaleStatus.Completed
                && s.CreatedAt >= from
                && s.CreatedAt <= to);

        var totalSales = await salesQuery.SumAsync(s => (decimal?)s.Total, cancellationToken) ?? 0m;

        var totalCosts = await _dbContext.SaleDetails
            .AsNoTracking()
            .Where(d => d.Sale != null
                && d.Sale.BranchId == currentBranchId.Value
                && d.Sale.Status == SaleStatus.Completed
                && d.Sale.CreatedAt >= from
                && d.Sale.CreatedAt <= to)
            .SumAsync(d => (decimal?)(d.Quantity * d.UnitCost), cancellationToken) ?? 0m;

        var grossProfit = totalSales - totalCosts;

        return Ok(new
        {
            success = true,
            data = new
            {
                branchId = currentBranchId.Value,
                from,
                to,
                totalSales,
                totalCosts,
                grossProfit
            }
        });
    }

    [HttpGet("reports/z-report")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetZReport([FromQuery] ZReportFilterRequest request, CancellationToken cancellationToken)
    {
        if (request.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "El parámetro BranchId es obligatorio para generar el Cierre Z." }
            });
        }

        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar reportes Z." }
            });
        }

        var isAdmin = string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && request.BranchId != currentBranchId.Value)
        {
            return Forbid();
        }

        var branchId = request.BranchId;
        var reportDate = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = new DateTimeOffset(reportDate.Year, reportDate.Month, reportDate.Day, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var completedSales = _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.BranchId == branchId
                && s.Status == SaleStatus.Completed
                && s.CreatedAt >= dayStart
                && s.CreatedAt < dayEnd);

        var refundedSales = _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.BranchId == branchId
                && (s.Status == SaleStatus.Refunded || s.IsRefunded)
                && s.CreatedAt >= dayStart
                && s.CreatedAt < dayEnd);

        var grossSales = await completedSales.SumAsync(s => (decimal?)s.SubTotal, cancellationToken) ?? 0m;
        var discounts = await completedSales.SumAsync(s => (decimal?)s.Discount, cancellationToken) ?? 0m;
        var refunds = await refundedSales.SumAsync(s => (decimal?)s.Total, cancellationToken) ?? 0m;
        var ticketCount = await completedSales.CountAsync(cancellationToken);
        var netSales = grossSales - discounts - refunds;

        var paymentBreakdown = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.Sale != null
                && p.Sale.BranchId == branchId
                && p.Sale.Status == SaleStatus.Completed
                && p.Sale.CreatedAt >= dayStart
                && p.Sale.CreatedAt < dayEnd)
            .GroupBy(p => p.Method)
            .Select(g => new
            {
                method = g.Key.ToString(),
                amount = g.Sum(x => x.Amount)
            })
            .OrderBy(x => x.method)
            .ToListAsync(cancellationToken);

        var cashIn = await _dbContext.CashMovements
            .AsNoTracking()
            .Where(m => m.Session != null
                && m.Session.BranchId == branchId
                && m.Type == CashMovementType.CashIn
                && m.CreatedAt >= dayStart
                && m.CreatedAt < dayEnd)
            .SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;

        var cashOut = await _dbContext.CashMovements
            .AsNoTracking()
            .Where(m => m.Session != null
                && m.Session.BranchId == branchId
                && m.Type == CashMovementType.CashOut
                && m.CreatedAt >= dayStart
                && m.CreatedAt < dayEnd)
            .SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;

        var branch = await _coreDbContext.Branches
            .AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                branchId,
                branchName = branch?.Name ?? "Sucursal",
                date = reportDate.ToString("yyyy-MM-dd"),
                generatedAt = DateTimeOffset.UtcNow,
                grossSales,
                discounts,
                refunds,
                netSales,
                ticketCount,
                paymentBreakdown,
                cashMovements = new
                {
                    cashIn,
                    cashOut,
                    net = cashIn - cashOut
                }
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
            .Where(s => s.SessionId == activeSessionId.Value && s.Status != SaleStatus.Pending)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                id = s.Id,
                createdAt = s.CreatedAt,
                subTotal = s.SubTotal,
                discount = s.Discount,
                tax = s.Tax,
                total = s.Total,
                isRefunded = s.IsRefunded,
                status = s.Status.ToString(),
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

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingSales(CancellationToken cancellationToken)
    {
        var currentBranchId = _currentUser.BranchId;
        var currentUserId = _currentUser.Id;

        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar ventas en espera." }
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
                data = Array.Empty<object>()
            });
        }

        var pendingSales = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.SessionId == activeSessionId.Value && s.Status == SaleStatus.Pending)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                id = s.Id,
                sessionId = s.SessionId,
                branchId = s.BranchId,
                customerId = s.CustomerId,
                createdAt = s.CreatedAt,
                subTotal = s.SubTotal,
                discount = s.Discount,
                tax = s.Tax,
                total = s.Total,
                details = s.Details.Select(d => new
                {
                    id = d.Id,
                    productId = d.ProductId,
                    quantity = d.Quantity,
                    unitPrice = d.UnitPrice,
                    discountAmount = d.DiscountAmount
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        var pendingCustomerIds = pendingSales
            .Where(s => s.customerId.HasValue)
            .Select(s => s.customerId!.Value)
            .Distinct()
            .ToList();

        var customersById = pendingCustomerIds.Count == 0
            ? new Dictionary<Guid, object>()
            : await _coreDbContext.Customers
                .AsNoTracking()
                .Where(c => pendingCustomerIds.Contains(c.Id))
                .ToDictionaryAsync(
                    c => c.Id,
                    c => (object)new { id = c.Id, name = c.Name, documentNumber = c.DocumentNumber },
                    cancellationToken);

        var data = pendingSales.Select(s => new
        {
            s.id,
            s.sessionId,
            s.branchId,
            s.customerId,
            customer = s.customerId.HasValue && customersById.TryGetValue(s.customerId.Value, out var customerData)
                ? customerData
                : null,
            s.createdAt,
            s.subTotal,
            s.discount,
            s.tax,
            s.total,
            s.details
        });

        return Ok(new
        {
            success = true,
            data
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

    [HttpGet("sessions/current/close-summary")]
    public async Task<IActionResult> GetCurrentCloseSummary(CancellationToken cancellationToken)
    {
        var currentBranchId = _currentUser.BranchId;
        var currentUserId = _currentUser.Id;

        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar el resumen de cierre." }
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

        var session = await _dbContext.CashRegisterSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.UserId == currentUserId.Value && s.BranchId == currentBranchId.Value && s.IsOpen,
                cancellationToken);

        if (session == null)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Sales.NoActiveSession", message = "No existe una sesión activa para la sucursal actual." }
            });
        }

        var cashSalesTotal = await _dbContext.Payments
            .Where(p => p.Sale != null && p.Sale.SessionId == session.Id && p.Method == PaymentMethod.Cash && p.Amount > 0m)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var cashRefundsTotal = await _dbContext.Payments
            .Where(p => p.Sale != null && p.Sale.SessionId == session.Id && p.Method == PaymentMethod.Cash && p.Amount < 0m)
            .SumAsync(p => (decimal?)-p.Amount, cancellationToken) ?? 0m;

        var manualCashInTotal = await _dbContext.CashMovements
            .Where(m => m.SessionId == session.Id && m.Type == CashMovementType.CashIn)
            .SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;

        var manualCashOutTotal = await _dbContext.CashMovements
            .Where(m => m.SessionId == session.Id && m.Type == CashMovementType.CashOut)
            .SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;

        var finalBalanceExpected = session.InitialBalance + cashSalesTotal - cashRefundsTotal + manualCashInTotal - manualCashOutTotal;

        return Ok(new
        {
            success = true,
            data = new
            {
                sessionId = session.Id,
                initialBalance = session.InitialBalance,
                cashSalesTotal,
                cashRefundsTotal,
                manualCashInTotal,
                manualCashOutTotal,
                finalBalanceExpected
            }
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

    [HttpPost("sessions/current/movements")]
    public async Task<IActionResult> RegisterCashMovement([FromBody] RegisterCashMovementRequest request)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para registrar movimientos de caja." }
            });
        }

        if (!Enum.TryParse<CashMovementType>(request.Type, ignoreCase: true, out var movementType))
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Sales.InvalidMovementType", message = "El tipo de movimiento debe ser CashIn o CashOut." }
            });
        }

        var command = new RegisterCashMovementCommand(
            currentBranchId.Value,
            movementType,
            request.Amount,
            request.Reason
        );

        var result = await _sender.Send(command);

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
            data = new { id = result.Value }
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

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompletePendingSale(Guid id, [FromBody] CompletePendingSaleRequest request)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para completar ventas en espera." }
            });
        }

        var command = new CompletePendingSaleCommand(
            id,
            currentBranchId.Value,
            request.CustomerId,
            request.Discount,
            request.Details,
            request.Payments
        );

        var result = await _sender.Send(command);

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
            data = new { id = result.Value }
        });
    }

    [HttpPost("{id}/refund")]
    public async Task<IActionResult> RefundSale(Guid id, [FromBody] RefundSaleRequest request)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para procesar devoluciones." }
            });
        }

        var command = new RefundSaleCommand(id, request.Reason);
        var result = await _sender.Send(command);

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
            message = "Devolución procesada exitosamente.",
            data = new { saleId = result.Value }
        });
    }
}

public record RefundSaleRequest(string Reason = "Devolución");
public record RegisterCashMovementRequest(decimal Amount, string Reason, string Type);
public record CompletePendingSaleRequest(Guid? CustomerId, decimal Discount, List<CreateSaleDetailDto> Details, List<CreateSalePaymentDto> Payments);
public record ProfitabilityFilterRequest(DateTimeOffset? From, DateTimeOffset? To);
public record ZReportFilterRequest(Guid BranchId, DateOnly? Date);
