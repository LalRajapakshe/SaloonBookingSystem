using Microsoft.EntityFrameworkCore;
using SalonBooking.Persistence.Context;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

using SalonBooking.Application.Common.Settings;
using SalonBooking.Application.Interfaces;
using SalonBooking.Infrastructure.Authentication;
using SalonBooking.API.Controllers;
using SalonBooking.Infrastructure.Services;

using FluentValidation;
using FluentValidation.AspNetCore;
using SalonBooking.Application.Features.Customer.Validators;

using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Identity;
using SalonBooking.Domain.Entities;


var builder = WebApplication.CreateBuilder(args);


var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is missing. Set ConnectionStrings:DefaultConnection with user secrets or environment variables.");
}

var jwtSettings =
    builder.Configuration.GetSection("JwtSettings");

var secretKey =
    jwtSettings["SecretKey"];

if (string.IsNullOrWhiteSpace(secretKey))
{
    throw new InvalidOperationException(
        "JWT Secret Key is missing. Set JwtSettings:SecretKey with user secrets or environment variables.");
}

var key =
    Encoding.UTF8.GetBytes(secretKey);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

builder.Services.AddScoped<IJwtTokenService,    JwtTokenService>();    

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();    
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ItenantService, TenantService>();
builder.Services.AddScoped<IBranchService, BranchService>();


builder.Services.AddScoped<IEmployeeService, EmployeeService>();

builder.Services.AddScoped<IServiceCategoryService, ServiceCategoryService>();
builder.Services.AddScoped<IServiceService, ServiceService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer =
                    jwtSettings["Issuer"],

                ValidAudience =
                    jwtSettings["Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(key),

                NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };

        options.MapInboundClaims = true;
    });    

    builder.Services.AddAuthorization();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddDbContext<SalonBookingDbContext>(
    options =>
        options.UseSqlServer(connectionString));
//builder.Services.AddDbContext<SalonBookingDbContext>(options =>
//{
//    options.UseSqlServer(
//        builder.Configuration.GetConnectionString("DefaultConnection"))
//        .EnableSensitiveDataLogging()
//        .LogTo(Console.WriteLine, LogLevel.Information);
//});


builder.Services.AddOpenApi();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Enter: Bearer {your JWT token}"
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                        new OpenApiReference
                        {
                            Type =
                                ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
        app.UseSwagger();
    app.UseSwaggerUI();
    await DevelopmentAdminSeeder.SeedAsync(app.Services);
   // app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();



