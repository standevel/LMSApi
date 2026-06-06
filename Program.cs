using LMS.Api.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration, "Serilog")
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is not configured");

builder.Services
    .AddApplicationCore()
    .AddApplicationDatabase(connectionString, builder.Environment.IsDevelopment())
    .AddApplicationSecurity(builder.Configuration);

var app = builder.Build();

await app.EnsureDatabaseInitializedAsync();

app.UseSerilogRequestLogging();

app.UseApplicationMiddleware()
   .MapApplicationEndpoints();

app.Run();

public partial class Program
{
}
