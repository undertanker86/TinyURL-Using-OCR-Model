using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System;
using System.Net;
using System.Threading.Tasks;

namespace ApiGateway
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Add controllers if needed
            services.AddControllers()
                .AddNewtonsoftJson(); // For JSON handling with Newtonsoft

            // CORS configuration
            services.AddCors(opt =>
            {
                opt.AddPolicy("AllowAll", p =>
                    p.AllowAnyOrigin()
                     .AllowAnyMethod()
                     .AllowAnyHeader());
            });

            // Register health checks if needed
            services.AddHealthChecks();

            // Add Ocelot services
            services.AddOcelot(Configuration);

            // Optional: Add memory cache
            services.AddMemoryCache();

            // Optional: Add response compression
            services.AddResponseCompression();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            logger.LogInformation("Configuring middleware pipeline...");

            // Global exception handler - must be first
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.ContentType = "application/json";

                    var exceptionHandlerPathFeature =
                        context.Features.Get<IExceptionHandlerPathFeature>();

                    var exception = exceptionHandlerPathFeature?.Error;

                    // Log the exception
                    logger.LogError(exception, "Unhandled exception occurred: {Message}", exception?.Message);

                    await context.Response.WriteAsync(
                        System.Text.Json.JsonSerializer.Serialize(
                            new
                            {
                                error = "An unexpected error occurred",
                                detail = env.IsDevelopment() ? exception?.ToString() : null
                            }));
                });
            });

            // Developer Exception Page in Development
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Optional: Response compression
            app.UseResponseCompression();

            // HTTPS Redirection if needed
            // app.UseHttpsRedirection();

            // Static Files if needed
            app.UseStaticFiles();

            // CORS policy - must be before Routing
            app.UseCors("AllowAll");

            // Routing is required
            app.UseRouting();

            // Authentication & Authorization if needed
            // app.UseAuthentication();
            // app.UseAuthorization();

            // Define endpoints
            app.UseEndpoints(endpoints =>
            {
                // Add controllers if you have them
                endpoints.MapControllers();

                // Add health checks endpoint
                endpoints.MapHealthChecks("/health");

                // Default root endpoint
                endpoints.MapGet("/", async context =>
                {
                    logger.LogInformation("Root endpoint requested");
                    await context.Response.WriteAsync("✅ API Gateway is running!");
                });
            });

            // Initialize Ocelot - must be after UseEndpoints
            try
            {
                logger.LogInformation("Initializing Ocelot middleware...");

                var ocelotTask = app.UseOcelot();

                // Use Wait() on a separate task to avoid deadlocks
                Task.Run(() => ocelotTask.Wait()).Wait();

                logger.LogInformation("Ocelot middleware initialized successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize Ocelot middleware");
                throw; // Rethrow to global error handler
            }

            logger.LogInformation("Middleware pipeline configured successfully");
        }
    }
}