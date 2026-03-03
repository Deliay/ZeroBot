namespace ZeroBot.Endfield.Api.Skland.Authorize;

public class DesRuleEntry
{
    public string? Cipher { get; set; }
    public required int IsEncrypt { get; set; }
    public string? Key { get; set; }
    public required string ObfuscatedName { get; set; }
}



public class UserCredential : IEquatable<UserCredential>
{
    public required DateTimeOffset TokenExpiredAt { get; set; }
    public required string RefreshToken { get; set; }
    public required string Cred { get; set; }
    public required string DeviceId { get; set; }

    public bool Equals(UserCredential? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Cred == other.Cred;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((UserCredential)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Cred);
    }
}