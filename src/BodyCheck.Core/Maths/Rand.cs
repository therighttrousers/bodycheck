namespace BodyCheck.Core.Maths;

/// <summary>
/// Random numbers that are the same on every run and every .NET version, from a string seed: FNV-1a hashes the seed,
/// and SplitMix64's finalizer makes each number from the last. .NET's string hashes differ from run to run, and its
/// seeded <see cref="System.Random"/> isn't promised to stay the same across versions.
/// </summary>
public sealed class Rand
{
    public static ulong Fnv1a(string s)
    {
        ulong hash = 14695981039346656037;
        foreach (char c in s)
        {
            hash = (hash ^ c) * 1099511628211;
        }
        return hash;
    }

    public static ulong SplitMix64(ulong x)
    {
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EB;
        return x ^ (x >> 31);
    }

    private ulong _current;

    public Rand(string seed)
    {
        _current = Fnv1a(seed);
    }

    public ulong NextUlong() => _current = SplitMix64(_current);

    public float NextFloat() => NextUlong() / 18446744073709551616.0f;
}
