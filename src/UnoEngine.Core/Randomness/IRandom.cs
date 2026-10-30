namespace UnoEngine.Core.Randomness;

/// <summary>Source of randomness for shuffling, injected so games can be replayed and tested.</summary>
public interface IRandom
{
    /// <summary>A number from 0 up to, but not including, <paramref name="maxExclusive"/>.</summary>
    int Next(int maxExclusive);
}

public sealed class SeededRandom(int? seed = null) : IRandom
{
    private readonly Random _random = seed is { } s ? new Random(s) : new Random();

    public int Next(int maxExclusive) => _random.Next(maxExclusive);
}

public static class ShuffleExtensions
{
    /// <summary>Fisher–Yates shuffle in place.</summary>
    public static void Shuffle<T>(this IList<T> items, IRandom random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
