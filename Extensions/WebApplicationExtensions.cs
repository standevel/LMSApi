using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Security;
using Microsoft.AspNetCore.Diagnostics;
using Scalar.AspNetCore; // Uncomment this if the Scalar package is installed and provides the extension
using System.Threading;

namespace LMS.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task<WebApplication> EnsureDatabaseInitializedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
        await dbInitializer.InitializeAsync(CancellationToken.None);
        return app;
    }

    public static WebApplication UseApplicationMiddleware(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }
        app.UseCors(ServiceCollectionExtensions.FrontendCorsPolicy);

        // Serve static files from uploads directory (must be early to bypass all auth)
        var storageBasePath = app.Configuration["FileStorage:BasePath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(storageBasePath),
            RequestPath = "/uploads"
        });

        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
                var errorMessage = app.Environment.IsDevelopment()
                    ? exceptionFeature?.Error.Message ?? "An unexpected error occurred."
                    : "An unexpected error occurred.";

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var response = ApiResponse<object>.Fail(
                    message: "Request failed",
                    status: StatusCodes.Status500InternalServerError,
                    error: new ApiError("server_error", errorMessage));

                await context.Response.WriteAsJsonAsync(response);
            });
        });

        app.UseStatusCodePages(async statusCodeContext =>
        {
            var httpContext = statusCodeContext.HttpContext;
            var status = httpContext.Response.StatusCode;

            if (status < 400)
            {
                return;
            }

            var (code, message) = status switch
            {
                StatusCodes.Status401Unauthorized => ("unauthorized", "Authentication is required to access this resource."),
                StatusCodes.Status403Forbidden => ("forbidden", "You do not have permission to access this resource."),
                StatusCodes.Status404NotFound => ("not_found", "The requested resource was not found."),
                _ => ("request_failed", "Request failed.")
            };

            httpContext.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                message: message,
                status: status,
                error: new ApiError(code, message));

            await httpContext.Response.WriteAsJsonAsync(response);
        });

        app.UseAuthentication();
        app.UseMiddleware<ApiReplayPreventionMiddleware>();
        app.UseMiddleware<UserProvisioningMiddleware>();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapApplicationEndpoints(this WebApplication app)
    {
        app.UseFastEndpoints(c =>
        {
            c.Serializer.Options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();

            app.MapScalarApiReference("/docs", options =>
            {
                options.Title = "LMS API";
                options.DarkMode = true;
                options.Theme = ScalarTheme.DeepSpace;
            }).AllowAnonymous();
        }

        return app;
    }
}
