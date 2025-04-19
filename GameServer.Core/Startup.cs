using System.Text;
using GameServer.Core.Hubs;
using GameServer.Core.Services;
using GameServer.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace GameServer.Core
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            
            // Add SignalR
            services.AddSignalR();
            
            // Add CORS
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder =>
                {
                    builder
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .WithOrigins(Configuration["AllowedOrigins"].Split(','))
                        .AllowCredentials();
                });
            });
            
            // Add JWT Authentication
            var key = Encoding.ASCII.GetBytes(Configuration["Jwt:Secret"]);
            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = false;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
                
                // Configure JWT Bearer to work with SignalR
                x.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/gamehub"))
                        {
                            context.Token = accessToken;
                        }
                        
                        return Task.CompletedTask;
                    }
                };
            });
            
            // Add Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Game Server API", Version = "v1" });
                
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
            
            // Configure game state persistence
            services.Configure<GameStatePersistenceOptions>(options => {
                options.PersistenceDirectory = Path.Combine(
                    Directory.GetCurrentDirectory(), 
                    Configuration.GetValue<string>("GameStatePersistenceDirectory", "GameStates"));
            });
            
            // Register services
            services.AddSingleton<PlayerManager>();
            services.AddSingleton<GameRoomManager>();
            services.AddSingleton<ExtensionLoader>(provider => 
                new ExtensionLoader(
                    provider.GetRequiredService<ILogger<ExtensionLoader>>(),
                    Configuration["ExtensionsPath"]));
            services.AddSingleton<ExtensionManager>();
            services.AddSingleton<MatchmakingService>();
            services.AddSingleton<AnalyticsService>();
            services.AddSingleton<GameStatePersistenceService>();
            services.AddScoped<UserService>();
            services.AddScoped<DatabaseGameHistoryService>();
        }

        // In the Configure method, add this after the app configuration but before the endpoint routing
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Game Server API v1"));
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseCors("CorsPolicy");
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<GameHub>("/gamehub");
            });
        
            // Initialize extension manager
            var extensionManager = serviceProvider.GetRequiredService<ExtensionManager>();
            extensionManager.Initialize();
            
            // Restore game states on startup
            var gameRoomManager = serviceProvider.GetRequiredService<GameRoomManager>();
            Task.Run(async () => {
                await gameRoomManager.RestoreGameStatesAsync();
            }).Wait();
        }
    }
}