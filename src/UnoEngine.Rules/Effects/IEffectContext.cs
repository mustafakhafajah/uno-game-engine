using UnoEngine.Core.Cards;

namespace UnoEngine.Rules.Effects;

/// <summary>
/// What a card ability is allowed to do to the game. The engine implements it; effects never see
/// the managers behind it, so new effects can be written without knowing how the engine works.
/// </summary>
public interface IEffectContext
{
    /// <summary>The seat of the player who played the card.</summary>
    int CurrentSeat { get; }

    int PlayerCount { get; }
    Card PlayedCard { get; }

    /// <summary>The seat <paramref name="steps"/> places after the current player, in the direction of play.</summary>
    int NextSeat(int steps = 1);

    /// <summary>All other seats in play order, starting with the next player.</summary>
    IReadOnlyList<int> OtherSeats();

    string PlayerName(int seat);
    int HandSize(int seat);

    /// <summary>The played card's current value for an attribute (including a chosen colour).</summary>
    string? ActiveValue(string attribute);

    /// <summary>Every value an attribute takes on the cards in this game.</summary>
    IReadOnlyList<string> ValuesOf(string attribute);

    void Skip(int count);
    void Reverse();
    void PlayAgain();
    void DrawCards(int seat, int count);

    /// <summary>
    /// The next player draws <paramref name="count"/> cards. With stacking switched on and
    /// <paramref name="losesTurn"/> set, the penalty is passed on instead so they can answer it.
    /// </summary>
    void PenalizeNextPlayer(int count, bool losesTurn);

    void SetActiveAttribute(string attribute, string value);
    void SwapHands(int seatA, int seatB);

    /// <summary>Every player passes their hand to the next player (or the previous one).</summary>
    void RotateHands(bool forward);

    /// <summary>Moves every card in a hand with the given attribute value to the discard pile.</summary>
    int DiscardMatching(int seat, string attribute, string value);
}
