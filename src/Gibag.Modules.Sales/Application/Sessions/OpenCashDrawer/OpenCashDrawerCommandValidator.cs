using FluentValidation;

namespace Gibag.Modules.Sales.Application.Sessions.OpenCashDrawer;

public class OpenCashDrawerCommandValidator : AbstractValidator<OpenCashDrawerCommand>
{
    public OpenCashDrawerCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("La sucursal es requerida.");
        RuleFor(x => x.InitialAmount).GreaterThanOrEqualTo(0).WithMessage("El monto inicial no puede ser negativo.");
    }
}
