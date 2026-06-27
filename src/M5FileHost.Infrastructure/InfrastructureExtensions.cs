using M5FileHost.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace M5FileHost.Infrastructure;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddM5Infrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
        var redisConnection = configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString, postgres => postgres.EnableRetryOnFailure(5)));
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddOptions<UploadOptions>().Bind(configuration.GetSection(UploadOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ClamAvOptions>().Bind(configuration.GetSection(ClamAvOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IFileTypeDetector, FileTypeDetector>();
        services.AddSingleton<IProcessingQueue, RedisProcessingQueue>();
        services.AddSingleton<IMalwareScanner, ClamAvScanner>();
        services.AddScoped<IFileProcessor, FileProcessor>();
        return services;
    }
}
