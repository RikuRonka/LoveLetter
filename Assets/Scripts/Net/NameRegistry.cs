using System.Collections.Generic;

public static class NameRegistry
{
    static readonly HashSet<string> _inUse =
        new(System.StringComparer.InvariantCultureIgnoreCase);

    public static string Sanitize(string s)
    {
        s = string.IsNullOrWhiteSpace(s) ? "Player" : s.Trim();
        if (s.Length > 20) s = s[..20];
        return s;
    }

    public static bool IsTaken(string name) => _inUse.Contains(name);
    public static bool Reserve(string name) => _inUse.Add(name);
    public static void Release(string name) { if (!string.IsNullOrWhiteSpace(name)) _inUse.Remove(name); }
}