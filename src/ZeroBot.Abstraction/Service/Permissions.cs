namespace ZeroBot.Abstraction.Service;

public class Permissions : Dictionary<string, HashSet<string>>
{
    public bool Has(string permission, string principal)
    {
        return ContainsKey(permission) && this[permission].Contains(principal);
    }
    
    public bool Grant(string permission, string principal)
    {
        if (!ContainsKey(permission)) Add(permission, []);
        return base[permission].Add(principal);
    }

    public bool Revoke(string permission, string principal)
    {
        if (!TryGetValue(permission, out var set)) return true;
        return !set.Contains(principal) || set.Remove(principal);
    }
}
