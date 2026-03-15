using FluentAssertions;
using Gibag.Modules.Inventory.Application.Products.CreateProduct;
using Gibag.Modules.Inventory.Domain;
using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gibag.Modules.Inventory.Tests.Application;

public class CreateProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithDuplicateSKU_ShouldReturnFailureResult()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var existingSku = "SKU-12345";

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .Options;

        var tenantServiceMock = new Mock<ITenantService>();
        tenantServiceMock.Setup(x => x.CurrentTenantId).Returns(tenantId);

        using (var context = new InventoryDbContext(options, tenantServiceMock.Object))
        {
            // Seed a valid category
            var category = new Category(tenantId, "Electronics", null);
            // We need to force the ID to match our categoryId since Category constructor generates a new one
            typeof(Category).GetProperty("Id")?.SetValue(category, categoryId);
            context.Categories.Add(category);

            // Seed an existing product with the target SKU
            var existingProduct = new Product(tenantId, categoryId, "Existing Product", existingSku, null, 10, 5);
            context.Products.Add(existingProduct);
            
            await context.SaveChangesAsync();
        }

        using (var context = new InventoryDbContext(options, tenantServiceMock.Object))
        {
            var handler = new CreateProductCommandHandler(context, tenantServiceMock.Object);
            var command = new CreateProductCommand(
                categoryId, 
                "New Product", 
                existingSku, // Duplicate SKU
                null, 
                20, 
                10
            );

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be("Inventory.SKUExists");
            result.ErrorMessage.Should().Contain(existingSku);
        }
    }
}
