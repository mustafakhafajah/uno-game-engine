using UnoEngine.Core.Cards;
using UnoEngine.Core.Players;
using UnoEngine.Engine.Mediator;

namespace UnoEngine.Engine.Managers;

/// <summary>Owns the seats, the hands and who has called UNO.</summary>
public sealed class PlayerManager(IGameMediator mediator) : GameManager(mediator)
{
    private readonly List<Player> _players = [];

    public IReadOnlyList<Player> Players => _players;
    public int Count => _players.Count;
    public Player this[int seat] => _players[seat];

    public void Seat(IReadOnlyList<string> names)
    {
        _players.Clear();
        _players.AddRange(names.Select((name, seat) => new Player(seat, name)));
    }

    public void Give(int seat, IEnumerable<Card> cards) => _players[seat].Give(cards);

    public Card? Find(int seat, int cardId) => _players[seat].Find(cardId);

    public Card Take(int seat, int cardId) => _players[seat].Take(cardId);

    public IReadOnlyList<Card> TakeMatching(int seat, string attribute, string value) =>
        _players[seat].TakeWhere(c => string.Equals(c.Definition.AttributeOrNull(attribute), value, StringComparison.OrdinalIgnoreCase));

    public void SwapHands(int seatA, int seatB)
    {
        var a = _players[seatA].TakeAll();
        var b = _players[seatB].TakeAll();
        _players[seatA].Give(b);
        _players[seatB].Give(a);
    }

    /// <summary>Each seat in <paramref name="order"/> passes its hand to the seat after it (the last to the first).</summary>
    public void RotateHands(IReadOnlyList<int> order)
    {
        var hands = order.Select(seat => _players[seat].TakeAll()).ToList();
        for (var i = 0; i < order.Count; i++)
            _players[order[(i + 1) % order.Count]].Give(hands[i]);
    }

    public IReadOnlyList<Card> CollectAllCards() => _players.SelectMany(p => p.TakeAll()).ToList();

    public bool CanCallUno(int seat, bool isTheirTurn)
    {
        var count = _players[seat].Hand.Count;
        return !_players[seat].CalledUno && (count == 1 || (count == 2 && isTheirTurn));
    }

    public void CallUno(int seat) => _players[seat].CallUno();

    /// <summary>One card left and UNO not called: another player can catch them.</summary>
    public bool IsUnoExposed(int seat) => _players[seat].Hand.Count == 1 && !_players[seat].CalledUno;

    public void ResetScores()
    {
        foreach (var player in _players)
            player.ResetScore();
    }

    public void AddScore(int seat, int points) => _players[seat].AddScore(points);
}
