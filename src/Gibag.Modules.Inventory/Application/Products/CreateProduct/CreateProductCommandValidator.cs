using FluentValidation;

namespace Gibag.Modules.Inventory.Application.Products.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("La categoría es obligatoria.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).WithMessage("El nombre es obligatorio y máximo 150 caracteres.");
        RuleFor(x => x.SKU).NotEmpty().MaximumLength(50).WithMessage("El SKU es obligatorio y máximo 50 caracteres.");
        RuleFor(x => x.Barcode).MaximumLength(100).WithMessage("El código de barras no puede exceder 100 caracteres.");
        RuleFor(x => x.BasePrice).GreaterThanOrEqualTo(0).WithMessage("El precio base no puede ser negativo.");
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).WithMessage("El costo no puede ser negativo.");
    }
}
