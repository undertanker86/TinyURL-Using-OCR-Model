// Startup.cs
using System;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using UrlShortener.Data;
using UrlShortener.Services;

namespace UrlShortener
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // Phương thức này được gọi bởi runtime. Dùng để thêm services vào container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Thêm DbContext vào DI container
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            // Thêm URL Shortener Service
            services.AddScoped<IUrlShortenerService>(provider =>
            {
                var dbContext = provider.GetRequiredService<AppDbContext>();
                var baseUrl = $"{Configuration["AppSettings:BaseUrl"]}";
                return new UrlShortenerService(dbContext, baseUrl);
            });

            // Thêm ExpiredUrlCleanupService
            services.AddHostedService<ExpiredUrlCleanupService>();

            // Thêm HttpClient
            services.AddHttpClient();

            // Cấu hình CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder =>
                {
                    builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

            // Cấu hình Authentication với custom handler
            services.AddAuthentication("NodeJs")
                .AddScheme<AuthenticationSchemeOptions, NodeJsAuthenticationHandler>("NodeJs", null);

            services.AddControllers();

            // Cấu hình Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "URL Shortener API", Version = "v1" });

                // Thêm JWT Authentication vào Swagger
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
            });
        }
        // Phương thức này được gọi bởi runtime. Dùng để cấu hình HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "URL Shortener API v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}