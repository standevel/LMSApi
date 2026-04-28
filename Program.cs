using LMS.Api.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// Configure Serilog for file and console logging
var logPath = Path.Combine(AppContext.BaseDirectory, "Logs", "admission-{Date}.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is not configured");

builder.Services
    .AddApplicationCore()
    .AddApplicationDatabase(connectionString)
    .AddApplicationSecurity(builder.Configuration);

var app = builder.Build();

await app.EnsureDatabaseInitializedAsync();

app.UseApplicationMiddleware()
   .MapApplicationEndpoints();

app.Run();

public partial class Program
{
}
