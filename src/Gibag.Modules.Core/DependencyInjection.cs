using System.Reflection;
using FluentValidation;
using Gibag.Modules.Core.Application.Behaviors;
using Gibag.Modules.Core.Application.Interfaces;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Modules.Core.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gibag.Modules.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Infrastructure
        services.AddDbContext<CoreDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IJwtProvider, JwtProvider>();

        // Application (MediatR & FluentValidation)
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
