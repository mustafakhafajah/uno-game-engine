using UnoEngine.Core.Cards;
using UnoEngine.Core.Events;
using UnoEngine.Rules.Effects;

namespace UnoEngine.Engine.Mediator;

/// <summary>
/// What a card's abilities can do, translated into calls on the right managers.
/// Effects only see this interface, never the managers.
/// </summary>
internal sealed class EffectContext(GameMediator game, Card playedCard) : IEffectContext
{
    public int CurrentSeat => game.Turns.CurrentSeat;
    public int PlayerCount => game.Players.Count;
    public Card PlayedCard { get; } = playedCard;

    public int NextSeat(int steps = 1) => game.Turns.NextSeat(steps);

    public IReadOnlyList<int> OtherSeats() => game.Turns.OrderFrom(CurrentSeat).Skip(1).ToList();

    public string PlayerName(int seat) => game.Players[seat].Name;

    public int HandSize(int seat) => game.Players[seat].Hand.Count;

    public string? ActiveValue(string attribute) =>
        ReferenceEquals(game.Deck.TopCard, PlayedCard)
            ? game.Deck.TopAttributes.GetValueOrDefault(attribute)
            : PlayedCard.Definition.AttributeOrNull(attribute);

    public IReadOnlyList<string> ValuesOf(string attribute) => game.Definition.ValuesOf(attribute);

    public void Skip(int count) => game.Turns.Skip(count);

    public void Reverse()
    {
        if (PlayerCount == 2 && game.Settings.TwoPlayerReverseActsAsSkip)
            game.Turns.Skip(1);
        else
            game.Turns.Reverse();
    }

    public void PlayAgain() => game.Turns.GrantPlayAgain();

    public void DrawCards(int seat, int count) => game.GiveCards(seat, count, DrawReason.CardEffect);

    public void PenalizeNextPlayer(int count, bool losesTurn)
    {
        if (losesTurn && game.Settings.StackDrawCards)
        {
            game.Rules.AddPendingDraw(count);
            return;
        }

        DrawCards(NextSeat(), count);
        if (losesTurn)
            Skip(1);
    }

    public void SetActiveAttribute(string attribute, string value)
    {
        game.Deck.ChooseAttribute(attribute, value);
        ((IGameMediator)game).Notify(new AttributeChosen(CurrentSeat, attribute, value));
    }

    public void SwapHands(int seatA, int seatB)
    {
        game.Players.SwapHands(seatA, seatB);
        ((IGameMediator)game).Notify(new HandsSwapped(seatA, seatB));
    }

    public void RotateHands(bool forward)
    {
        var order = game.Turns.OrderFrom(CurrentSeat).ToList();
        if (!forward)
            order.Reverse();
        game.Players.RotateHands(order);
        ((IGameMediator)game).Notify(new HandsRotated(forward));
    }

    public int DiscardMatching(int seat, string attribute, string value)
    {
        var cards = game.Players.TakeMatching(seat, attribute, value);
        game.Deck.DiscardUnderTop(cards);
        if (cards.Count > 0)
            ((IGameMediator)game).Notify(new CardsDiscarded(seat, cards.Count));
        return cards.Count;
    }
}
