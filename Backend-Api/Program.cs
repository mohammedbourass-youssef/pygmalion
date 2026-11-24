using Backend_Api.Repositories;
using Backend_Api.Services;
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
builder.Services.AddSingleton<IDateRepository, InMemoryDateRepository>();
builder.Services.AddScoped<IDateGenerationService, DateGenerationService>();
builder.Services.AddScoped<IFakeDataGenerator, FakeDataGenerator>();
builder.Services.AddScoped<CodeParsingService>();
builder.Services.AddScoped<IGeneratedDataFileService, GeneratedDataFileService>();
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseSerilogRequestLogging();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapControllers();

app.Run();
