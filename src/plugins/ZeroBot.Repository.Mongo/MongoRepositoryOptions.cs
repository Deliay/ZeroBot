namespace ZeroBot.Repository.Mongo;

public record MongoRepositoryOptions
{
    public string ConnectionString { get; init; } = "mongodb://127.0.0.1:27017";
    public int PoolSize { get; init; } = 10;
}
