using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroBot.Utility;

public static class ComponentsExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection ConfigureComponent<T>() where T : class, IComponentInitializer
        {
            return services
                .AddSingleton<T>()
                .AddSingleton<IComponentInitializer, T>(sp => sp.GetRequiredService<T>());
        }

        public IServiceCollection ConfigureComponent<T>(Func<IServiceProvider, T> factory)
            where T : class, IComponentInitializer
        {
            return services
                .AddSingleton(factory)
                .AddSingleton<IComponentInitializer, T>(sp => sp.GetRequiredService<T>());
        }
        
        public IServiceCollection ConfigureComponent<TAbstract, TImpl>()
            where TAbstract : class, IComponentInitializer
            where TImpl : class, TAbstract
        {
            return services
                .AddSingleton<TAbstract, TImpl>()
                .AddSingleton<IComponentInitializer, TAbstract>(sp => sp.GetRequiredService<TAbstract>());
        }
        
        public IServiceCollection ConfigureComponent<TAbstract, TImpl>(Func<IServiceProvider, TAbstract> factory)
            where TAbstract : class, IComponentInitializer
            where TImpl : class, TAbstract
        {
            return services
                .AddSingleton<TAbstract, TImpl>()
                .AddSingleton<IComponentInitializer, TAbstract>(factory);
        }
    }
}
