using FluentValidation;

namespace Gibag.Modules.Sales.Application.Sessions.CloseCashDrawer;

public class CloseCashDrawerCommandValidator : AbstractValidator<CloseCashDrawerCommand>
{
    public CloseCashDrawerCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("La sucursal es requerida.");
        RuleFor(x => x.FinalBalanceEncounted).GreaterThanOrEqualTo(0).WithMessage("El monto final contado no puede ser negativo.");
    }
}
