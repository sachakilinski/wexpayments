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
using System.Text.Json;
using Wex.CorporatePayments.Api.Health;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog programmatically
builder.Host.UseSerilog((context, configuration) =>
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithThreadId()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}"));

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        // Fix for .NET 10 PipeWriter issue - use synchronous writer
        options.JsonSerializerOptions.WriteIndented = false;
    });

// Configure distributed cache (in-memory for self-contained requirement)
builder.Services.AddDistributedMemoryCache();

// Configure Polly policies for HTTP resilience
var retryPolicy = Policy<HttpResponseMessage>
    .Handle<HttpRequestException>()
    .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var circuitBreakerPolicy = Policy<HttpResponseMessage>
    .Handle<HttpRequestException>()
    .AdvancedCircuitBreakerAsync(0.5, TimeSpan.FromSeconds(30), 5, TimeSpan.FromSeconds(30));

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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Wex Corporate Payments API",
        Version = "v1",
        Description = "API for managing corporate payment transactions with currency conversion",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Wex Corporate Payments Team",
            Email = "support@wex.com"
        }
    });

    // Include XML Comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    // Add security definition
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

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

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<TreasuryApiHealthCheck>("treasury-api", tags: new[] { "external", "api" })
    .AddDbContextCheck<ApplicationDbContext>("database", tags: new[] { "database" })
    .AddSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=purchases.db", "sqlite-db", tags: new[] { "database" })
    .AddUrlGroup(new Uri(builder.Configuration["TreasuryApi:BaseUrl"] ?? "https://api.fiscal.treasury.gov"), "treasury-api-uri", tags: new[] { "external", "api" });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Wex Corporate Payments API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Wex Corporate Payments API Documentation";
        c.DefaultModelsExpandDepth(-1); // Hide models by default
        c.DefaultModelExpandDepth(1);
        c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Add Health Check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            Status = report.Status.ToString(),
            TotalDuration = report.TotalDuration.TotalMilliseconds,
            Results = report.Entries.Select(e => new
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Duration = e.Value.Duration.TotalMilliseconds,
                Description = e.Value.Description,
                Data = e.Value.Data,
                Exception = e.Value.Exception?.Message,
                Tags = e.Value.Tags
            })
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

// Individual health check endpoints
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") || check.Tags.Contains("database")
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Basic liveness check
});

app.MapControllers();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
}

app.Run();
