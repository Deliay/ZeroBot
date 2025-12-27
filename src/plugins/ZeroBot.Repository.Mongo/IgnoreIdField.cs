using Milky.Net.Model;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace ZeroBot.Repository.Mongo;

public class IgnoreIdField : IClassMapConvention
{
    public string Name { get; } = nameof(IgnoreIdField);
    public void Apply(BsonClassMap classMap)
    {
        classMap.SetIgnoreExtraElements(true);
    }

    private static bool TypePredicate(Type type)
    {
        return type.IsAssignableTo(typeof(Event));
    }

    public static void Register()
    {
        ConventionRegistry.Register("IgnoreIdField", new ConventionPack { new IgnoreIdField() }, IgnoreIdField.TypePredicate);
    }
}
