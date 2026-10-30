using UnoEngine.Core.Cards;
using UnoEngine.Core.Events;
using UnoEngine.Engine.Mediator;

namespace UnoEngine.Engine.Managers;

/// <summary>Owns whose turn it is, the direction of play, pending skips, extra turns and what was drawn this turn.</summary>
public sealed class TurnManager(IGameMediator mediator) : GameManager(mediator)
{
    private int _playerCount;
    private int _pendingSkips;
    private bool _playAgain;

    public int CurrentSeat { get; private set; }
    public bool Clockwise { get; private set; } = true;

    /// <summary>The current player has drawn this turn (they may now pass).</summary>
    public bool HasDrawn { get; private set; }

    /// <summary>The drawn card the player may still play, if any.</summary>
    public Card? PlayableDrawnCard { get; private set; }

    public void Reset(int playerCount, int startingSeat)
    {
        _playerCount = playerCount;
        CurrentSeat = startingSeat;
        Clockwise = true;
        ClearTurnState();
    }

    public int SeatAfter(int seat, int steps = 1)
    {
        var offset = (Clockwise ? steps : -steps) % _playerCount;
        return ((seat + offset) % _playerCount + _playerCount) % _playerCount;
    }

    public int NextSeat(int steps = 1) => SeatAfter(CurrentSeat, steps);

    /// <summary>Every seat in play order, starting with <paramref name="seat"/>.</summary>
    public IReadOnlyList<int> OrderFrom(int seat) =>
        Enumerable.Range(0, _playerCount).Select(step => SeatAfter(seat, step)).ToList();

    public void Skip(int count) => _pendingSkips += count;

    public void Reverse()
    {
        Clockwise = !Clockwise;
        Notify(new DirectionReversed(Clockwise));
    }

    public void GrantPlayAgain()
    {
        _playAgain = true;
        Notify(new PlayAgainGranted(CurrentSeat));
    }

    public void MarkDrawn(Card? playableDrawnCard)
    {
        HasDrawn = true;
        PlayableDrawnCard = playableDrawnCard;
    }

    /// <summary>Used when a card acts for someone other than the current player (the dealer's first card).</summary>
    public void SetCurrent(int seat) => CurrentSeat = seat;

    /// <summary>Moves to the next player, applying skips and extra turns, and returns the new seat.</summary>
    public int EndTurn()
    {
        if (_playAgain)
        {
            ClearTurnState();
            return CurrentSeat;
        }

        for (var i = 1; i <= _pendingSkips; i++)
            Notify(new PlayerSkipped(SeatAfter(CurrentSeat, i)));
        CurrentSeat = SeatAfter(CurrentSeat, 1 + _pendingSkips);
        ClearTurnState();
        return CurrentSeat;
    }

    private void ClearTurnState()
    {
        _pendingSkips = 0;
        _playAgain = false;
        HasDrawn = false;
        PlayableDrawnCard = null;
    }
}
