using EmberFramework;
using EmberFramework.Layer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZeroBot.Abstraction;
using ZeroBot.Abstraction.Bot;
using ZeroBot.Core.Services;
using ZeroBot.Milky;

TypedPluginLoader.Register<MilkyPlugin>();

Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
var root = RootBuilder
    .Boot()
    .Infrastructures((services, config) =>
    {
        services.AddLogging(logger => logger.AddConsole());
        services.AddSingleton<ILifetimeManager, LifetimeManager>();
        services.AddSingleton<IBotContext, BotContext>();
        services.AddOptions();
    })
    .UseLoader<TypedPluginLoader>()
    .Build();
    
await root.RunAsync();
