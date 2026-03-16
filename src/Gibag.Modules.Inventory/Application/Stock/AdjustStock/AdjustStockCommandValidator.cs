using FluentValidation;

namespace Gibag.Modules.Inventory.Application.Stock.AdjustStock;

public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("La sucursal es obligatoria.");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("El producto es obligatorio.");
        RuleFor(x => x.QuantityDelta)
            .NotEqual(0).WithMessage("La cantidad a ajustar no puede ser cero.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo es obligatorio.")
            .MaximumLength(180).WithMessage("El motivo no puede exceder 180 caracteres.");
    }
}
