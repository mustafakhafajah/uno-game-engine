using UnoEngine.Core.Cards;
using UnoEngine.Core.Choices;
using UnoEngine.Engine.Mediator;

namespace UnoEngine.Engine.Views;

public sealed record PlayerView(int Seat, string Name, IReadOnlyList<Card> Hand, int Score, bool CalledUno);

/// <summary>A read-only snapshot of the table: everything a screen or a bot needs, nothing it can change.</summary>
public sealed record GameView(
    GamePhase Phase,
    int Round,
    int CurrentSeat,
    bool Clockwise,
    Card? TopCard,
    IReadOnlyDictionary<string, string> TopAttributes,
    int DrawPileCount,
    int DiscardPileCount,
    int PendingDraw,
    ChoiceRequest? PendingChoice,
    bool CurrentPlayerHasDrawn,
    int? PlayableDrawnCardId,
    IReadOnlyList<PlayerView> Players,
    int? WinnerSeat)
{
    public PlayerView Current => Players[CurrentSeat];

    internal static GameView Of(GameMediator game) => new(
        game.Phase,
        game.Scores.Round,
        game.Turns.CurrentSeat,
        game.Turns.Clockwise,
        game.Deck.TopCard,
        game.Deck.TopAttributes,
        game.Deck.DrawPileCount,
        game.Deck.DiscardPileCount,
        game.Rules.PendingDraw,
        game.Choices.Pending,
        game.Turns.HasDrawn,
        game.Turns.PlayableDrawnCard?.InstanceId,
        game.Players.Players.Select(p => new PlayerView(p.Seat, p.Name, p.Hand.ToList(), p.Score, p.CalledUno)).ToList(),
        game.WinnerSeat);
}
