using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ZeroBot.Utility.FileWatcher;

public static class JsonFileWatcherExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection ConfigureJsonConfig<T>(string path, T defaultValue,
            CancellationToken cancellationToken = default) where T : class
        {
            services.AddSingletonComponent<JsonConfig<T>>(_ =>
                new JsonConfig<T>(path, defaultValue, cancellationToken));
            
            services.AddSingleton<IJsonConfig<T>, JsonConfig<T>>(sp => sp.GetRequiredService<JsonConfig<T>>());
            
            services.AddTransient<IOptions<T>>(sp =>
                new OptionsWrapper<T>(sp.GetRequiredService<IJsonConfig<T>>().Current!));

            return services;
        }
    }
}
