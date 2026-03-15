using FluentValidation;

namespace Gibag.Modules.Sales.Application.Sales.CreateSale;

public class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("La sesión de caja es obligatoria.");
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("La sucursal es obligatoria.");
        RuleFor(x => x.Total).GreaterThanOrEqualTo(0).WithMessage("El total no puede ser negativo.");
        
        RuleFor(x => x.Details).NotEmpty().WithMessage("La venta debe tener al menos un detalle.");
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
    }
}
