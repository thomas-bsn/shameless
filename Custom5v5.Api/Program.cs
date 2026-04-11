using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;

using Custom5v5.Api.Interfaces;
using Custom5v5.Api.Services;
using Custom5v5.Application.Interfaces;
using Custom5v5.Application.Matches;
using Custom5v5.Application.Services;
using Custom5v5.Infrastructure.Data;
using Custom5v5.Infrastructure.Data.Repositories;
using Custom5v5.Infrastructure.Options;
using Custom5v5.Infrastructure.Repositories;
using Custom5v5.Infrastructure.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL") 
                  ?? builder.Configuration["DATABASE_URL"];

var hasDatabaseUrl = !string.IsNullOrWhiteSpace(databaseUrl);
var allowedOrigins = builder.Configuration["AllowedOrigins"] ?? "http://localhost:3000";

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins.Split(","))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // ✅ AJOUT
    });
});

// Background job
builder.Services.AddHostedService<RefreshRanksJob>();

// Controllers + JSON
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
    );

// EF Core
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL") 
                ?? builder.Configuration["DATABASE_URL"];
    
    string connection;
    if (!string.IsNullOrWhiteSpace(dbUrl))
    {
        var uri = new Uri(dbUrl);
        var userInfo = uri.UserInfo.Split(':');
        connection = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={Uri.UnescapeDataString(userInfo[1])}";
    }
    else
    {
        var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        connection = $"Host={db.Host};Port={db.Port};Database={db.Database};Username={db.Username};Password={db.Password}";
    }
    
    options.UseNpgsql(connection).UseSnakeCaseNamingConvention();
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Custom5v5.Api", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Options
builder.Services.AddOptions<RiotApiOptions>()
    .Bind(builder.Configuration.GetSection("Auth:Riot"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "Auth:Riot:ApiKey missing")
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Auth:Riot:BaseUrl missing")
    .ValidateOnStart();

builder.Services.AddOptions<DiscordOAuthOptions>()
    .Bind(builder.Configuration.GetSection("Auth:Discord"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "Auth:Discord:ClientId missing")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), "Auth:Discord:ClientSecret missing")
    .Validate(o => !string.IsNullOrWhiteSpace(o.RedirectUri), "Auth:Discord:RedirectUri missing")
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Auth:Jwt"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Auth:Jwt:Issuer missing")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Auth:Jwt:Audience missing")
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "Auth:Jwt:SigningKey missing")
    .ValidateOnStart();

// Valide DatabaseOptions seulement si pas de DATABASE_URL
if (!hasDatabaseUrl)
{
    builder.Services.AddOptions<DatabaseOptions>()
        .Bind(builder.Configuration.GetSection("Database"))
        .Validate(o => !string.IsNullOrWhiteSpace(o.Username), "Database:Username missing")
        .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "Database:Password missing")
        .ValidateOnStart();
}
else
{
    builder.Services.AddOptions<DatabaseOptions>()
        .Bind(builder.Configuration.GetSection("Database"));
}

// Auth JWT
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// HTTP Clients
builder.Services.AddHttpClient<IRiotService, RiotService>((sp, client) =>
{
    var riot = sp.GetRequiredService<IOptions<RiotApiOptions>>().Value;
    client.BaseAddress = new Uri(riot.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("X-Riot-Token", riot.ApiKey);
});

builder.Services.AddHttpClient<IDiscordOAuthClient, DiscordOAuthClient>((sp, client) =>
{
    client.BaseAddress = new Uri("https://discord.com/api/");
});

// DI
builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IMatchProvider, MatchProvider>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<MatchProcessor>();
builder.Services.AddScoped<ICustomGameRepository, CustomGameRepository>();
builder.Services.AddScoped<ICustomGameParticipantRepository, CustomGameParticipantRepository>();
builder.Services.AddScoped<IPlayerMonthlyCostRepository, PlayerMonthlyCostRepository>();
builder.Services.AddScoped<CustomGameService>();
builder.Services.AddScoped<CostCalculationService>();

builder.Services.AddSingleton<IOAuthStateStore, OAuthStateStore>();
builder.Services.AddSingleton<IJwtIssuer, JwtIssuer>();
builder.Services.AddSingleton<PollStore>();

// App
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

using (var scope = app.Services.CreateScope())
{
    var playerService = scope.ServiceProvider.GetRequiredService<IPlayerService>();
    await playerService.RefreshAllRanksAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();