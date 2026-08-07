using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Paages.Infrastructure.Data;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddPaagesDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var dbPath = configuration["Database:Path"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Paages", "paages.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddSingleton<SqlitePragmaInterceptor>();

        services.AddDbContext<PaagesDbContext>((provider, options) =>
        {
            options.UseSqlite($"Data Source={dbPath}");
            options.AddInterceptors(provider.GetRequiredService<SqlitePragmaInterceptor>());
        });

        return services;
    }
}