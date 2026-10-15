using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shameless.Infrastructure.Data;
using Shameless.Infrastructure.Options;

namespace Shameless.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        
            string connection;
            if (!string.IsNullOrWhiteSpace(databaseUrl))
            {
                // Parse Railway-style URL
                connection = BuildConnectionStringFromUrl(databaseUrl);
            }
            else
            {
                // Use DatabaseOptions (dev local)
                var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
                connection = BuildConnectionString(db);
            }
        
            options.UseNpgsql(connection).UseSnakeCaseNamingConvention();
        });

        return services;
    }

    private static string BuildConnectionStringFromUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={Uri.UnescapeDataString(userInfo[1])}";
    }

    private static string BuildConnectionString(DatabaseOptions options)
    {
        return $"Host={options.Host};Port={options.Port};Database={options.Database};Username={options.Username};Password={options.Password}";
    }
}