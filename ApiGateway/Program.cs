using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ApiGateway
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Add process ID and exit handler
            Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => {
                Console.WriteLine("Process is exiting. Reason unknown.");
                Console.WriteLine("Stack trace:");
                Console.WriteLine(Environment.StackTrace);
                Console.WriteLine("Press any key to continue...");
                try { Console.ReadKey(); } catch { /* ignore if no console available */ }
            };

            Console.WriteLine("Starting API Gateway...");

            // Setup unhandled exception handler
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                Console.Error.WriteLine($"CRITICAL ERROR: Unhandled exception: {eventArgs.ExceptionObject}");

                // In development, pause before exit to see the error
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    Console.WriteLine("Press any key to exit...");
                    try { Console.ReadKey(); } catch { /* ignore if no console available */ }
                }
            };

            try
            {
                var host = CreateHostBuilder(args).Build();

                // Get services
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

                // Register application lifetime callbacks
                lifetime.ApplicationStarted.Register(() =>
                    logger.LogInformation("API Gateway application started"));

                lifetime.ApplicationStopping.Register(() =>
                {
                    logger.LogInformation("API Gateway application is stopping");
                    Console.WriteLine("API Gateway stopping. Press any key if you can see this...");
                    try { Console.ReadKey(true); } catch { /* ignore */ }
                });

                lifetime.ApplicationStopped.Register(() =>
                {
                    logger.LogInformation("API Gateway application has stopped");
                    Console.WriteLine("API Gateway stopped. Press any key if you can see this...");
                    try { Console.ReadKey(true); } catch { /* ignore */ }
                });

                // Run the host
                Console.WriteLine("API Gateway host built, starting to run...");
                await host.RunAsync();

                // This line should only execute when the host is explicitly stopped
                Console.WriteLine("API Gateway host has shut down normally");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Critical error during startup: {ex}");

                // In development, pause before exit to see the error
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    Console.WriteLine("Press any key to exit...");
                    try { Console.ReadKey(); } catch { /* ignore if no console available */ }
                }
            }

            // Extra safeguard to keep console window open in case of errors
            if (Debugger.IsAttached)
            {
                Console.WriteLine("Debug mode detected. Press any key to finally exit...");
                try { Console.ReadKey(); } catch { /* ignore if no console available */ }
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.SetBasePath(ctx.HostingEnvironment.ContentRootPath)
                       .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                       .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                       .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
                       .AddEnvironmentVariables();
                })
                .ConfigureWebHostDefaults(web =>
                {
                    web.UseKestrel(options =>
                    {
                        options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
                        options.Limits.MaxConcurrentConnections = 100;
                        options.Limits.MaxConcurrentUpgradedConnections = 100;
                        options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
                        options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
                    })
                    .UseStartup<Startup>();
                    //.UseUrls("http://localhost:5006");  // Listen on port 5006
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.AddEventSourceLogger();

                    // Configure log levels if needed
                    logging.AddFilter("Microsoft", LogLevel.Warning);
                    logging.AddFilter("System", LogLevel.Warning);
                    logging.AddFilter("ApiGateway", LogLevel.Information);

                    // Add file logging if needed
                    // logging.AddFile("Logs/apigateway-{Date}.log");
                });
    }
}