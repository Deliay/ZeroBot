using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Endfield.Api.Skland;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Api.Extension;

public static class PluginInjectionExtensions
{
    extension(IServiceCollection services)
    {
        private IServiceCollection AddEndfieldApiCoreService()
        {
            return services
                .AddSingleton<HypergryphClient>()
                .AddSingleton<CredentialManager>();
        }
        public IServiceCollection AddEndfieldApi<TCredential>() where TCredential : class, ICredentialRepository
        {
            return services
                .AddEndfieldApiCoreService()
                .AddSingleton<ICredentialRepository, TCredential>();
        }
        public IServiceCollection AddEndfieldApi<TCredential>(Func<IServiceProvider, TCredential> factory) where TCredential : class, ICredentialRepository
        {
            return services
                .AddEndfieldApiCoreService() 
                .AddSingleton<ICredentialRepository, TCredential>(factory);
        }
    }
}