using Backend_Api.Repositories;
using Backend_Api.Services;
using Backend_Api.Data;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var otlpEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
var seqEndpoint = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "pygmalion-backend-api")
    .WriteTo.Console()
    .WriteTo.Seq(seqEndpoint, apiKey: context.Configuration["Seq:ApiKey"]));

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<PygmalionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Pygmalion")));
builder.Services.AddSingleton<IDateRepository, InMemoryDateRepository>();
builder.Services.AddScoped<IDateGenerationService, DateGenerationService>();
builder.Services.AddScoped<IFakeDataGenerator, FakeDataGenerator>();
builder.Services.AddScoped<CodeParsingService>();
builder.Services.AddScoped<IGeneratedDataFileService, GeneratedDataFileService>();
builder.Services.AddScoped<IGeneratedDataService, GeneratedDataService>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "pygmalion-backend-api",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
    .WithTracing(tracing => tracing
        .AddSource(PygmalionTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Protocol = OtlpExportProtocol.Grpc;
        }))
    .WithMetrics(metrics => metrics
        .AddMeter(PygmalionTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Protocol = OtlpExportProtocol.Grpc;
        }));

var app = builder.Build();

// Bootstrap the initial schema; the unique CodeHash index makes generation idempotent.
await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<PygmalionDbContext>();
    await database.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseSerilogRequestLogging();

app.MapGet("/health", async (
    PygmalionDbContext database,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        return await database.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "healthy" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Backend readiness check could not connect to PostgreSQL.");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllers();

app.Run();
