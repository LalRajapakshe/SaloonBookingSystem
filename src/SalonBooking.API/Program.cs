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

using Microsoft.OpenApi.Models;


var builder = WebApplication.CreateBuilder(args);


var jwtSettings =
    builder.Configuration.GetSection("JwtSettings");

var secretKey =
    jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException(
        "JWT Secret Key is missing.");

var key =
    Encoding.UTF8.GetBytes(secretKey);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

builder.Services.AddScoped<IJwtTokenService,    JwtTokenService>();    

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();    
builder.Services.AddScoped<ICustomerService, CustomerService>();

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
                    new SymmetricSecurityKey(key)
            };
    });    

    builder.Services.AddAuthorization();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddDbContext<SalonBookingDbContext>(
    options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString(
                "DefaultConnection")));

builder.Services.AddOpenApi();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

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
   // app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();



