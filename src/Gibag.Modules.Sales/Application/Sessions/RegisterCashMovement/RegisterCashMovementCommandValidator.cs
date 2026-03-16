using FluentValidation;

namespace Gibag.Modules.Sales.Application.Sessions.RegisterCashMovement;

public class RegisterCashMovementCommandValidator : AbstractValidator<RegisterCashMovementCommand>
{
    public RegisterCashMovementCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("La sucursal es requerida.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo es obligatorio.")
            .MaximumLength(200).WithMessage("El motivo no puede superar los 200 caracteres.");
    }
}
