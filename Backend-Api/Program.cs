using Backend_Api.Repositories;
using Backend_Api.Services;
using Backend_Api.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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

app.MapControllers();

app.Run();
