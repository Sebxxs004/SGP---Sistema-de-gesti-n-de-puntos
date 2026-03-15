using Gibag.Api.Middlewares;
using Gibag.Api.Services;
using Gibag.Modules.Core;
using Gibag.Modules.Inventory;
using Gibag.Modules.Sales;
using Gibag.Modules.Sales.Application.Interfaces;
using Gibag.Shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Use Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Add Custom Middlewares
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

app.Run();
