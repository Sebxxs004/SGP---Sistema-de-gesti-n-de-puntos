using Gibag.Modules.Core.Domain;
using Gibag.Modules.Core.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Core.Presentation.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly CoreDbContext _dbContext;

    public CustomersController(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Customers.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(searchTerm) ||
                (c.DocumentNumber != null && c.DocumentNumber.ToLower().Contains(searchTerm)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerListItemDto(
                c.Id,
                c.Name,
                c.DocumentNumber,
                c.Email,
                c.Phone,
                c.Address,
                c.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                items,
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            }
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCustomerById(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerListItemDto(
                c.Id,
                c.Name,
                c.DocumentNumber,
                c.Email,
                c.Phone,
                c.Address,
                c.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        if (customer == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Customer.NotFound", message = "El cliente no existe en este tenant." }
            });
        }

        return Ok(new
        {
            success = true,
            data = customer
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] UpsertCustomerRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        var document = NormalizeOptional(request.DocumentNumber);
        if (!string.IsNullOrWhiteSpace(document))
        {
            var exists = await _dbContext.Customers.AnyAsync(c => c.DocumentNumber == document, cancellationToken);
            if (exists)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "Customer.DocumentExists", message = "Ya existe un cliente con ese documento." }
                });
            }
        }

        var customer = new Customer(
            tenantId: Guid.Empty,
            name: request.Name,
            documentNumber: request.DocumentNumber,
            email: request.Email,
            phone: request.Phone,
            address: request.Address);

        await _dbContext.Customers.AddAsync(customer, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/v1/customers/{customer.Id}", new
        {
            success = true,
            data = new CustomerListItemDto(
                customer.Id,
                customer.Name,
                customer.DocumentNumber,
                customer.Email,
                customer.Phone,
                customer.Address,
                customer.IsActive)
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCustomer(Guid id, [FromBody] UpsertCustomerRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Customer.NotFound", message = "El cliente no existe en este tenant." }
            });
        }

        var document = NormalizeOptional(request.DocumentNumber);
        if (!string.IsNullOrWhiteSpace(document))
        {
            var exists = await _dbContext.Customers.AnyAsync(c => c.Id != id && c.DocumentNumber == document, cancellationToken);
            if (exists)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "Customer.DocumentExists", message = "Ya existe un cliente con ese documento." }
                });
            }
        }

        customer.Update(request.Name, request.DocumentNumber, request.Email, request.Phone, request.Address);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new CustomerListItemDto(
                customer.Id,
                customer.Name,
                customer.DocumentNumber,
                customer.Email,
                customer.Phone,
                customer.Address,
                customer.IsActive)
        });
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateCustomer(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Customer.NotFound", message = "El cliente no existe en este tenant." }
            });
        }

        customer.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Cliente desactivado correctamente."
        });
    }

    private static object? ValidateRequest(UpsertCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new
            {
                success = false,
                error = new { code = "Validation.Invalid", message = "El nombre del cliente es obligatorio." }
            };
        }

        return null;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}

public sealed record UpsertCustomerRequest(
    string Name,
    string? DocumentNumber,
    string? Email,
    string? Phone,
    string? Address);

public sealed record CustomerListItemDto(
    Guid Id,
    string Name,
    string? DocumentNumber,
    string? Email,
    string? Phone,
    string? Address,
    bool IsActive);
