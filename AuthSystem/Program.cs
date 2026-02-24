
using AuthSystem.Models;
using AuthSystem.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.JSInterop.Infrastructure;
using System.Text;

namespace AuthSystem
{
    public class Program
    {
        public static void Main(string[] args)
        {
            DotNetEnv.Env.Load();
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            var dbconnectionString = Environment.GetEnvironmentVariable("MONGO_DB_URI");
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");


            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            builder.Services.Configure<DatabaseSettings>(options =>
            {
                options.ConnectionString = dbconnectionString ?? "mongodb://localhost:27017";
                options.DatabaseName = "Auth";
                options.Collection = "Users";
            });

            builder.Services.Configure<JwtSettings>(options =>
            {
                options.Key = jwtKey ?? "DefaultKeyForDevelopment123456789012345678901234567890";
                options.Issuer = "AuthSystem";
                options.Audience = "AuthSystemUsers";
                options.ExpireMinutes = 60;
            });

            builder.Services.AddSingleton<TokenService>();
            builder.Services.AddSingleton<UserService>();


            var key = Encoding.UTF8.GetBytes(jwtKey ?? "DefaultKeyForDevelopment123456789012345678901234567890");

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "AuthSystem",
                    ValidAudience = "AuthSystemUsers",
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };

                // Read token from cookie
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["auth_user"];
                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
