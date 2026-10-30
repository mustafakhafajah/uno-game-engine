using UnoEngine.Core.Cards;
using UnoEngine.Core.Choices;

namespace UnoEngine.Core.Events;

/// <summary>Something that happened in the game. Front-ends listen to these to update the screen.</summary>
public abstract record GameEvent;

public sealed record RoundStarted(int Round, int StartingSeat) : GameEvent;
public sealed record FirstCardFlipped(Card Card) : GameEvent;
public sealed record CardPlayed(int Seat, Card Card) : GameEvent;
public sealed record CardsDrawn(int Seat, int Count, DrawReason Reason) : GameEvent;
public sealed record TurnChanged(int Seat) : GameEvent;
public sealed record TurnPassed(int Seat) : GameEvent;
public sealed record PlayerSkipped(int Seat) : GameEvent;
public sealed record DirectionReversed(bool Clockwise) : GameEvent;
public sealed record PlayAgainGranted(int Seat) : GameEvent;
public sealed record DrawPenaltyStacked(int Total) : GameEvent;
public sealed record ChoiceRequested(ChoiceRequest Request) : GameEvent;
public sealed record ChoiceMade(int Seat, ChoiceKind Kind, string Option) : GameEvent;
public sealed record AttributeChosen(int Seat, string Attribute, string Value) : GameEvent;
public sealed record HandsSwapped(int SeatA, int SeatB) : GameEvent;
public sealed record HandsRotated(bool Forward) : GameEvent;
public sealed record CardsDiscarded(int Seat, int Count) : GameEvent;
public sealed record UnoCalled(int Seat) : GameEvent;
public sealed record UnoCaught(int CatcherSeat, int Seat, int Penalty) : GameEvent;
public sealed record DeckReshuffled(int CardCount) : GameEvent;
public sealed record RoundWon(int Seat, int Points, IReadOnlyDictionary<int, int> Scores) : GameEvent;
public sealed record GameWon(int Seat) : GameEvent;

public enum DrawReason
{
    Deal,
    Draw,
    CardEffect,
    StackedPenalty,
    UnoPenalty,
}
