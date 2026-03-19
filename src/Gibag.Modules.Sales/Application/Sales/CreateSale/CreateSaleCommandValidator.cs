using FluentValidation;
using Gibag.Modules.Sales.Domain;

namespace Gibag.Modules.Sales.Application.Sales.CreateSale;

public class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("La sesión de caja es obligatoria.");
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("La sucursal es obligatoria.");
        RuleFor(x => x.Total).GreaterThanOrEqualTo(0).WithMessage("El total no puede ser negativo.");
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).WithMessage("El descuento no puede ser negativo.");
        RuleFor(x => x.CustomerId)
            .Must(customerId => customerId == null || customerId != Guid.Empty)
            .WithMessage("El cliente seleccionado no es válido.");

        RuleFor(x => x.Details).NotEmpty().WithMessage("La venta debe tener al menos un detalle.");
        RuleForEach(x => x.Details).ChildRules(detail => 
        {
            detail.RuleFor(d => d.ProductId).NotEmpty();
            detail.RuleFor(d => d.Quantity).GreaterThan(0);
            detail.RuleFor(d => d.UnitPrice).GreaterThanOrEqualTo(0);
        });

        When(x => (x.Status ?? SaleStatus.Completed) == SaleStatus.Completed, () =>
        {
            RuleFor(x => x.Payments)
                .NotEmpty()
                .WithMessage("La venta completada debe incluir al menos un pago.");
        });

        When(x => (x.Status ?? SaleStatus.Completed) == SaleStatus.Pending, () =>
        {
            RuleFor(x => x.Payments)
                .Must(payments => payments.Count == 0)
                .WithMessage("Una venta en espera no debe registrar pagos todavía.");
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
