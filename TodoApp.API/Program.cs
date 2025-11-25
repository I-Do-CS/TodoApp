using Microsoft.AspNetCore.Identity;
using TodoApp.API.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure DbContext and Identity
builder.Services.AddConfiguredDbContext(builder.Configuration);
builder.Services.AddConfiguredIdentity();
builder.Services.AddRuntimeServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Apply DB migrations in development
    await app.ApplyMigrationsAsync();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
