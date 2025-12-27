using EmberFramework.Abstraction;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroBot.Utility;

public static class ComponentsExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSingletonComponent<T>() where T : class, IComponentInitializer
        {
            return services
                .AddSingleton<T>()
                .AddSingleton<IComponentInitializer, T>(sp => sp.GetRequiredService<T>());
        }

        public IServiceCollection AddSingletonComponent<T>(Func<IServiceProvider, T> factory)
            where T : class, IComponentInitializer
        {
            return services
                .AddSingleton(factory)
                .AddSingleton<IComponentInitializer, T>(sp => sp.GetRequiredService<T>());
        }
        
        public IServiceCollection AddSingletonComponent<TAbstract, TImpl>()
            where TAbstract : class, IComponentInitializer
            where TImpl : class, TAbstract
        {
            return services
                .AddSingleton<TAbstract, TImpl>()
                .AddSingleton<IComponentInitializer, TAbstract>(sp => sp.GetRequiredService<TAbstract>());
        }
        
        public IServiceCollection AddSingletonComponent<TAbstract, TImpl>(Func<IServiceProvider, TAbstract> factory)
            where TAbstract : class, IComponentInitializer
            where TImpl : class, TAbstract, IComponentInitializer
        {
            return services
                .AddSingleton<TAbstract, TImpl>()
                .AddSingleton<IComponentInitializer, TAbstract>(factory);
        }

        public IServiceCollection AddSingletonExecutable<TImpl>() where TImpl : class, IExecutable
        {
            return services.AddSingleton<TImpl>()
                .AddSingleton<IExecutable, TImpl>(sp => sp.GetRequiredService<TImpl>());
        }

        public IServiceCollection AddSingletonExecutable<TAbstract, TImpl>()
            where TAbstract : class
            where TImpl : class, IExecutable, TAbstract
        {
            return services.AddSingleton<TImpl>()
                .AddSingleton<TAbstract, TImpl>(sp => sp.GetRequiredService<TImpl>())
                .AddSingleton<IExecutable, TImpl>(sp => sp.GetRequiredService<TImpl>());
        }

        public IServiceCollection AddSingletonInfra<TImpl>() where TImpl : class, IInfrastructureInitializer
        {
            return services
                .AddSingleton<TImpl>()
                .AddSingleton<IInfrastructureInitializer, TImpl>(sp => sp.GetRequiredService<TImpl>());
        }

        public IServiceCollection AddSingletonInfra<TAbstrat, TImpl>()
            where TAbstrat : class
            where TImpl : class, TAbstrat, IInfrastructureInitializer
        {
            return services
                .AddSingleton<TImpl>()
                .AddSingleton<TAbstrat, TImpl>(sp => sp.GetRequiredService<TImpl>())
                .AddSingleton<IInfrastructureInitializer, TImpl>(sp => sp.GetRequiredService<TImpl>());
        }
    }
}
