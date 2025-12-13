using Microsoft.EntityFrameworkCore;
using Wex.CorporatePayments.Application.Interfaces;
using Wex.CorporatePayments.Application.UseCases;
using Wex.CorporatePayments.Application.Validators;
using Wex.CorporatePayments.Application.Behaviors;
using Wex.CorporatePayments.Infrastructure.Data;
using Wex.CorporatePayments.Infrastructure.Repositories;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=wex_corporate_payments.db"));

// Register repositories
builder.Services.AddScoped<IPurchaseRepository, PurchaseRepository>();

// Register use cases
builder.Services.AddScoped<IStorePurchaseTransactionUseCase, StorePurchaseTransactionUseCase>();

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(StorePurchaseTransactionUseCase).Assembly));

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<StorePurchaseCommandValidator>();
builder.Services.AddFluentValidationAutoValidation();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
}

app.Run();
