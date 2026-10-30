using UnoEngine.Core.Cards;

namespace UnoEngine.Core.Players;

/// <summary>A seat at the table. Only the engine changes a hand; everyone else reads it.</summary>
public sealed class Player(int seat, string name)
{
    private readonly List<Card> _hand = [];

    public int Seat { get; } = seat;
    public string Name { get; } = name;
    public IReadOnlyList<Card> Hand => _hand;
    public int Score { get; private set; }

    /// <summary>True once the player called UNO; cleared whenever they receive cards.</summary>
    public bool CalledUno { get; private set; }

    internal void Give(IEnumerable<Card> cards)
    {
        var before = _hand.Count;
        _hand.AddRange(cards);
        if (_hand.Count > before)
            CalledUno = false;
    }

    internal Card? Find(int instanceId) => _hand.Find(c => c.InstanceId == instanceId);

    internal Card Take(int instanceId)
    {
        var card = Find(instanceId) ?? throw new InvalidOperationException($"{Name} does not hold card #{instanceId}.");
        _hand.Remove(card);
        return card;
    }

    internal IReadOnlyList<Card> TakeAll()
    {
        var cards = _hand.ToList();
        _hand.Clear();
        return cards;
    }

    internal IReadOnlyList<Card> TakeWhere(Func<Card, bool> predicate)
    {
        var cards = _hand.Where(predicate).ToList();
        foreach (var card in cards)
            _hand.Remove(card);
        return cards;
    }

    internal void CallUno() => CalledUno = true;

    internal void AddScore(int points) => Score += points;

    internal void ResetScore() => Score = 0;
}
