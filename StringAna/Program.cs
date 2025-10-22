using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StringAnalyzerApi.Repositories;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Use camelCase JSON and keep dictionary keys as-is
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In-memory repository (singleton for demo)
builder.Services.AddSingleton<IStringRepository, InMemoryStringRepository>();
// Add a simple NLP parser service (stateless) — implemented inline in controller for simplicity

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.Run();
