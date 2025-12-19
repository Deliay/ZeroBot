using EmberFramework;
using EmberFramework.Abstraction;
using EmberFramework.Layer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZeroBot.Abstraction;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.ComfyUI;
using ZeroBot.Core.Services;
using ZeroBot.Milky;
using ZeroBot.PermissionCommandPlugin;
using ZeroBot.TestPlugin;

TypedPluginLoader.Register<MilkyPlugin>();
TypedPluginLoader.Register<TestPlugin>();
TypedPluginLoader.Register<ZeroBotComfyUiPlugin>();
TypedPluginLoader.Register<ZeroBotPermissionCommandPlugin>();

Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
var root = RootBuilder
    .Boot()
    .Infrastructures((services, config) =>
    {
        services.AddLogging(logger => logger.AddConsole());
        services.AddSingleton<ILifetimeManager, LifetimeManager>();
        services.AddSingleton<IBotContext, BotContext>();
        services.AddSingleton<IServiceManager, ServiceManager>();
        services.AddSingleton<CommandDispatcher>();
        services.AddSingleton<ICommandDispatcher>(sp => sp.GetRequiredService<CommandDispatcher>());
        services.AddSingleton<IInfrastructureInitializer>(sp => sp.GetRequiredService<CommandDispatcher>());
        services.AddSingleton<Permission>();
        services.AddSingleton<IPermission>(sp => sp.GetRequiredService<Permission>());
        services.AddSingleton<IInfrastructureInitializer>(sp => sp.GetRequiredService<Permission>());
        services.AddOptions();
    })
    .UseLoader<TypedPluginLoader>()
    .Build();
    
await root.RunAsync();
