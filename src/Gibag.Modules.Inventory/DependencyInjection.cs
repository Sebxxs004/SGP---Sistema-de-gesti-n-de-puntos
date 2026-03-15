using System.Reflection;
using FluentValidation;
using Gibag.Modules.Core.Application.Behaviors; 
// We are reusing the ValidationBehavior from Core. Ideally, this could be moved to Shared 
// if multiple modules use it. For now, referencing Core is acceptable or we duplicate it.
// To keep modules decoupled, we'll duplicate it here.
using Gibag.Modules.Inventory.Application.Behaviors;
using Gibag.Modules.Inventory.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gibag.Modules.Inventory;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        var assembly = Assembly.GetExecutingAssembly();
        
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
