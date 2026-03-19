using FluentValidation;
using Gibag.Modules.Sales.Domain;

namespace Gibag.Modules.Sales.Application.Sales.CompletePendingSale;

public class CompletePendingSaleCommandValidator : AbstractValidator<CompletePendingSaleCommand>
{
    public CompletePendingSaleCommandValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty().WithMessage("La venta es requerida.");
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("La sucursal es requerida.");
        RuleFor(x => x.CustomerId)
            .Must(customerId => customerId == null || customerId != Guid.Empty)
            .WithMessage("El cliente seleccionado no es válido.");
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).WithMessage("El descuento no puede ser negativo.");
        RuleFor(x => x.Details).NotEmpty().WithMessage("La venta debe tener al menos un detalle.");
        RuleFor(x => x.Payments).NotEmpty().WithMessage("La venta completada debe incluir al menos un pago.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId).NotEmpty();
            detail.RuleFor(d => d.Quantity).GreaterThan(0);
            detail.RuleFor(d => d.UnitPrice).GreaterThanOrEqualTo(0);
        });

        RuleForEach(x => x.Payments).ChildRules(payment =>
        {
            payment.RuleFor(p => p.Amount).GreaterThan(0);
            payment.RuleFor(p => p.Method).IsInEnum();
        });

        RuleFor(x => x.CustomerId)
            .NotNull()
            .When(x => x.Payments.Any(p => p.Method == PaymentMethod.Credit))
            .WithMessage("Las ventas a crédito requieren un cliente seleccionado.");
    }
}