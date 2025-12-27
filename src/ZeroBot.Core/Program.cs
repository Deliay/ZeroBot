using EmberFramework;
using EmberFramework.Abstraction;
using EmberFramework.Layer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZeroBot.Abstraction;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Abstraction.Service;
using ZeroBot.Bilibili;
using ZeroBot.ComfyUI;
using ZeroBot.Core.Services;
using ZeroBot.Milky;
using ZeroBot.PermissionCommandPlugin;
using ZeroBot.Repository.Mongo;
using ZeroBot.TestPlugin;
using ZeroBot.Utility;

TypedPluginLoader.Register<MilkyPlugin>();
TypedPluginLoader.Register<TestPlugin>();
TypedPluginLoader.Register<MongoRepositoryPlugin>();
TypedPluginLoader.Register<ZeroBotComfyUiPlugin>();
TypedPluginLoader.Register<ZeroBotPermissionCommandPlugin>();
TypedPluginLoader.Register<BiliBiliPlugin>();

Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
var root = RootBuilder
    .Boot()
    .Infrastructures((services, config) =>
    {
        services.AddLogging(logger => logger.AddConsole());
        services.AddSingleton<ILifetimeManager, LifetimeManager>();
        services.AddSingleton<IBotContext, BotContext>();
        services.AddSingleton<IServiceManager, ServiceManager>();
        services.AddSingletonInfra<ICommandDispatcher, CommandDispatcher>();
        services.AddSingletonInfra<IPermission, Permission>();
        services.AddOptions();
    })
    .UseLoader<TypedPluginLoader>()
    .Build();
    
await root.RunAsync();
