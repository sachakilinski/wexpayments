using Microsoft.EntityFrameworkCore;
using Wex.CorporatePayments.Application.Interfaces;
using Wex.CorporatePayments.Application.UseCases;
using Wex.CorporatePayments.Application.Validators;
using Wex.CorporatePayments.Application.Behaviors;
using Wex.CorporatePayments.Application.Services;
using Wex.CorporatePayments.Infrastructure.Data;
using Wex.CorporatePayments.Infrastructure.Repositories;
using Wex.CorporatePayments.Infrastructure.Clients;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Serilog;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog programmatically
builder.Host.UseSerilog((context, configuration) =>
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}"));

// Add services to the container.
builder.Services.AddControllers();

// Configure distributed cache (in-memory for self-contained requirement)
builder.Services.AddDistributedMemoryCache();

// Configure Polly policies for HTTP resilience
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

// Add HttpClient for TreasuryApiClient with Polly policies
builder.Services.AddHttpClient<ITreasuryApiClient, TreasuryApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["TreasuryApi:BaseUrl"] 
                               ?? throw new InvalidOperationException("TreasuryApi:BaseUrl não configurado."));
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreakerPolicy);

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

// Register services
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();

// Add MediatR with ValidationBehavior
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(StorePurchaseTransactionUseCase).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

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
