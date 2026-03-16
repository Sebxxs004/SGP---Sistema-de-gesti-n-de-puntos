using Gibag.Api.Infrastructure;
using Gibag.Api.Middlewares;
using Gibag.Api.Services;
using Gibag.Modules.Core;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Modules.Inventory;
using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Modules.Sales;
using Gibag.Modules.Sales.Application.Interfaces;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// CORS – allow the Vite dev server (and any future PWA/CDN URL)
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Register Scoped ITenantService and ICurrentUser (Using shared dummy implementation for now)
builder.Services.AddScoped<CurrentTenantService>();
builder.Services.AddScoped<ITenantService>(sp => sp.GetRequiredService<CurrentTenantService>());
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentTenantService>());

// Register Modules
builder.Services.AddCoreModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
builder.Services.AddSalesModule(builder.Configuration);

// Cross-Module Adapters
builder.Services.AddScoped<IInventoryService, SalesInventoryIntegrationService>();

// Configure JWT Authentication
var jwtSecret = builder.Configuration["JwtOptions:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtOptions:Issuer"],
            ValidAudience = builder.Configuration["JwtOptions:Audience"],
            RoleClaimType = ClaimTypes.Role,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Run migrations and seed initial data
if (!IsEfDesignTime(args) && await AreDatabasesReadyAsync(app.Services))
{
    await DbInitializer.SeedAsync(app.Services);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Use CORS before Auth
app.UseCors("FrontendPolicy");

// Use Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Add Custom Middlewares
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

app.Run();

static bool IsEfDesignTime(string[] startupArgs)
{
    var efDesignTimeFlags = new[]
    {
        "EF_DESIGN_TIME",
        "DOTNET_EF_DESIGNTIME",
        "DESIGN_TIME_BUILD"
    };

    if (efDesignTimeFlags.Any(key => string.Equals(Environment.GetEnvironmentVariable(key), "1", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(Environment.GetEnvironmentVariable(key), "true", StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    return startupArgs.Any(a => a.Contains("ef", StringComparison.OrdinalIgnoreCase) && a.Contains("design", StringComparison.OrdinalIgnoreCase));
}

static async Task<bool> AreDatabasesReadyAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var coreDb = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var salesDb = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

        var coreReady = await coreDb.Database.CanConnectAsync();
        var inventoryReady = await inventoryDb.Database.CanConnectAsync();
        var salesReady = await salesDb.Database.CanConnectAsync();

        if (coreReady && inventoryReady && salesReady)
        {
            return true;
        }

        logger.LogWarning("[Startup] Database is not ready yet. Skipping migrations and seed.");
        return false;
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[Startup] Failed while checking database availability. Skipping migrations and seed.");
        return false;
    }
}
