using Gibag.Modules.Core.Infrastructure;
using Gibag.Modules.Sales.Domain;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Gibag.Modules.Sales.Presentation.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize]
public class AccountsReceivableController : ControllerBase
{
    private const string ReceivableMarkerPrefix = "AR:";

    private readonly SalesDbContext _salesDbContext;
    private readonly CoreDbContext _coreDbContext;
    private readonly ICurrentUser _currentUser;

    public AccountsReceivableController(
        SalesDbContext salesDbContext,
        CoreDbContext coreDbContext,
        ICurrentUser currentUser)
    {
        _salesDbContext = salesDbContext;
        _coreDbContext = coreDbContext;
        _currentUser = currentUser;
    }

    [HttpGet("{id:guid}/receivables")]
    public async Task<IActionResult> GetCustomerReceivables(Guid id, [FromQuery] bool includePaid = false, CancellationToken cancellationToken = default)
    {
        var customerExists = await _coreDbContext.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == id, cancellationToken);

        if (!customerExists)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Customer.NotFound", message = "El cliente no existe en este tenant." }
            });
        }

        var query = _salesDbContext.AccountReceivables
            .AsNoTracking()
            .Where(ar => ar.CustomerId == id);

        if (!includePaid)
        {
            query = query.Where(ar => ar.Status != AccountReceivableStatus.Paid);
        }

        var receivables = await query
            .OrderBy(ar => ar.DueDate)
            .Select(ar => new
            {
                ar.Id,
                ar.SaleId,
                ar.TotalAmount,
                ar.PaidAmount,
                ar.Balance,
                ar.DueDate,
                status = ar.Status.ToString(),
                ar.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = receivables
        });
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> RegisterCustomerPayment(Guid id, [FromBody] RegisterReceivablePaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.Invalid", message = "El monto del abono debe ser mayor a cero." }
            });
        }

        var currentBranchId = _currentUser.BranchId;
        var currentUserId = _currentUser.Id;

        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para registrar abonos." }
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

        var customerExists = await _coreDbContext.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == id, cancellationToken);

        if (!customerExists)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Customer.NotFound", message = "El cliente no existe en este tenant." }
            });
        }

        var receivable = await _salesDbContext.AccountReceivables
            .FirstOrDefaultAsync(ar => ar.Id == request.AccountReceivableId && ar.CustomerId == id, cancellationToken);

        if (receivable == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Receivable.NotFound", message = "No se encontró la cuenta por cobrar indicada para el cliente." }
            });
        }

        var session = await _salesDbContext.CashRegisterSessions
            .FirstOrDefaultAsync(s => s.UserId == currentUserId.Value && s.BranchId == currentBranchId.Value && s.IsOpen, cancellationToken);

        if (session == null)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Sales.NoActiveSession", message = "No existe una sesión activa para registrar el abono." }
            });
        }

        var parsedMethod = PaymentMethod.Cash;
        if (!string.IsNullOrWhiteSpace(request.PaymentMethod)
            && !Enum.TryParse<PaymentMethod>(request.PaymentMethod, ignoreCase: true, out parsedMethod))
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Sales.InvalidPaymentMethod", message = "El método de pago del abono no es válido." }
            });
        }

        if (parsedMethod == PaymentMethod.Credit)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Sales.InvalidPaymentMethod", message = "Un abono no puede registrarse con método de pago Crédito." }
            });
        }

        try
        {
            receivable.RegisterPayment(request.Amount);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Receivable.InvalidPayment", message = ex.Message }
            });
        }

        var payment = new Payment(
            id: Guid.NewGuid(),
            tenantId: receivable.TenantId,
            saleId: receivable.SaleId,
            amount: request.Amount,
            method: parsedMethod);

        await _salesDbContext.Payments.AddAsync(payment, cancellationToken);

        var movementReason = BuildCashMovementReason(receivable.Id, request.Note);

        var movement = new CashMovement(
            receivable.TenantId,
            session.Id,
            CashMovementType.CashIn,
            request.Amount,
            movementReason);

        await _salesDbContext.CashMovements.AddAsync(movement, cancellationToken);

        await _salesDbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                receivableId = receivable.Id,
                receivable.SaleId,
                receivable.TotalAmount,
                receivable.PaidAmount,
                receivable.Balance,
                status = receivable.Status.ToString(),
                cashMovementId = movement.Id
            }
        });
    }

    [HttpGet("{id:guid}/payments/history")]
    public async Task<IActionResult> GetCustomerPaymentHistory(Guid id, CancellationToken cancellationToken = default)
    {
        var customerExists = await _coreDbContext.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == id, cancellationToken);

        if (!customerExists)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Customer.NotFound", message = "El cliente no existe en este tenant." }
            });
        }

        var receivableMap = await _salesDbContext.AccountReceivables
            .AsNoTracking()
            .Where(ar => ar.CustomerId == id)
            .Select(ar => new
            {
                ar.Id,
                ar.SaleId
            })
            .ToListAsync(cancellationToken);

        if (receivableMap.Count == 0)
        {
            return Ok(new
            {
                success = true,
                data = Array.Empty<object>()
            });
        }

        var receivableById = receivableMap.ToDictionary(x => x.Id, x => x.SaleId);

        var rawMovements = await _salesDbContext.CashMovements
            .AsNoTracking()
            .Where(m => m.Type == CashMovementType.CashIn && m.Reason.Contains(ReceivableMarkerPrefix))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Amount,
                m.CreatedAt,
                m.Reason
            })
            .ToListAsync(cancellationToken);

        var history = new List<object>();

        foreach (var movement in rawMovements)
        {
            var receivableId = TryExtractReceivableId(movement.Reason);
            if (receivableId == null || !receivableById.TryGetValue(receivableId.Value, out var saleId))
            {
                continue;
            }

            history.Add(new
            {
                cashMovementId = movement.Id,
                accountReceivableId = receivableId,
                saleId,
                amount = movement.Amount,
                registeredAt = movement.CreatedAt,
                note = CleanMovementReason(movement.Reason)
            });
        }

        return Ok(new
        {
            success = true,
            data = history
        });
    }

    private static string BuildCashMovementReason(Guid receivableId, string? note)
    {
        var marker = $"{ReceivableMarkerPrefix}{receivableId:D}";

        if (string.IsNullOrWhiteSpace(note))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{marker} | Abono cuenta por cobrar #{receivableId.ToString()[..8]}");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{marker} | {note.Trim()}");
    }

    private static Guid? TryExtractReceivableId(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var markerStart = reason.IndexOf(ReceivableMarkerPrefix, StringComparison.OrdinalIgnoreCase);
        if (markerStart < 0)
        {
            return null;
        }

        var idStart = markerStart + ReceivableMarkerPrefix.Length;
        if (reason.Length < idStart + 36)
        {
            return null;
        }

        var candidate = reason.Substring(idStart, 36);
        return Guid.TryParse(candidate, out var receivableId) ? receivableId : null;
    }

    private static string CleanMovementReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return string.Empty;
        }

        var separator = "|";
        var separatorIndex = reason.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex < 0 || separatorIndex + 1 >= reason.Length)
        {
            return reason.Trim();
        }

        return reason[(separatorIndex + 1)..].Trim();
    }
}

public sealed record RegisterReceivablePaymentRequest(
    Guid AccountReceivableId,
    decimal Amount,
    string? PaymentMethod,
    string? Note);
