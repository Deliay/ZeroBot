namespace ZeroBot.Repository.Mongo;

public record MongoRepositoryOptions
{
    public string? ConnectionString { get; init; }
    public int PoolSize { get; init; } = 10;
}
