using AuthSystem.Models;
using AuthSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AuthSystem
{
    public class Program
    {
        public static void Main(string[] args)
        {
            DotNetEnv.Env.Load();
            var builder = WebApplication.CreateBuilder(args);

            var dbconnectionString = Environment.GetEnvironmentVariable("MONGO_DB_URI");
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
            var googleClientId = Environment.GetEnvironmentVariable("CLIENT_ID") ?? "";
            var googleClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET") ?? "";

            builder.Services.AddControllers();

            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(
                    Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")))
                .SetApplicationName("AuthSystem");

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

            var key = Encoding.UTF8.GetBytes(jwtKey ?? "DefaultKeyForDevelopment123456789012345678901234567890");


            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            })
            .AddGoogle(opt =>
            {
                opt.ClientId = googleClientId;
                opt.ClientSecret = googleClientSecret;
                opt.CallbackPath = "/api/auth/signin-google";
                opt.SaveTokens = true;

                opt.CorrelationCookie.SameSite = SameSiteMode.None;
                opt.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                opt.CorrelationCookie.IsEssential = true;
                opt.CorrelationCookie.HttpOnly = true;
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

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["auth_user"];
                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddSingleton<TokenService>();
            builder.Services.AddSingleton<UserService>();
            builder.Services.AddAuthorization();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseForwardedHeaders();


            app.UseCors(policy =>
            {
                policy.WithOrigins("https://localhost:3000")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}