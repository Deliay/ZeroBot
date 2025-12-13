using EmberFramework;
using EmberFramework.Layer;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Abstraction;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Core.Services;

var root = RootBuilder
    .Boot()
    .Infrastructures((services, config) =>
    {
        services.AddSingleton<ILifetimeManager, LifetimeManager>();
        services.AddSingleton<IBotContext, BotContext>();
    })
    .UseLoader<PluginLoader>()
    .Build();
    
var lifetime = await root.ResolveServiceAsync<LifetimeManager>().FirstAsync();

await root.RunAsync(lifetime.CancellationToken);
