using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Repository.Mongo;

public class MongoRepositoryPlugin(IOptions<MongoRepositoryOptions> options, IBotContext botContext) : IPlugin.IWithInitializer
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(_ =>
        {
            if (options.Value.ConnectionString is null) throw new InvalidOperationException("Mongo connection string is null.");
            var mongoSettings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);
            mongoSettings.MaxConnectionPoolSize = options.Value.PoolSize;
            return mongoSettings;
        });
        services.AddSingleton<IMongoClient>(sp => new MongoClient(sp.GetRequiredService<MongoClientSettings>()));
        services.AddSingleton<MongoRepository>();

        return ValueTask.FromResult(services);
    }

    public ValueTask InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var repository = services.GetRequiredService<MongoRepository>();
        botContext.SetEventRepository(repository);
        return default;
    }
}
