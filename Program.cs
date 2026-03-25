using LMS.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

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
