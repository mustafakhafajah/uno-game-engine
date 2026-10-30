using UnoEngine.Core.Cards;
using UnoEngine.Core.Definitions;
using UnoEngine.Core.Events;
using UnoEngine.Engine.Mediator;
using UnoEngine.Rules.Matching;

namespace UnoEngine.Engine.Managers;

/// <summary>Decides whether a play is legal, and holds a stacked draw penalty waiting to be answered.</summary>
public sealed class RuleManager(IGameMediator mediator, IPlayRule playRule, GameSettings settings) : GameManager(mediator)
{
    public GameSettings Settings { get; } = settings;

    /// <summary>Cards the next player must draw unless they stack another draw card (stacking only).</summary>
    public int PendingDraw { get; private set; }

    /// <summary>Null when the card can be played; otherwise why not, in words a player understands.</summary>
    public string? WhyNotPlayable(Card card, IReadOnlyDictionary<string, string> topAttributes, bool hasDrawn, Card? playableDrawnCard)
    {
        if (hasDrawn && playableDrawnCard?.InstanceId != card.InstanceId)
            return playableDrawnCard is null
                ? "You already drew this turn; pass."
                : $"After drawing you may only play the card you drew ({playableDrawnCard.Label}), or pass.";
        if (PendingDraw > 0)
            return IsStackable(card) ? null : $"Answer the draw penalty with a draw card, or draw {PendingDraw}.";
        if (card.IsWild || playRule.Matches(card, topAttributes))
            return null;
        return $"{card} does not match the top card.";
    }

    public bool CanPlay(Card card, IReadOnlyDictionary<string, string> topAttributes, bool hasDrawn, Card? playableDrawnCard) =>
        WhyNotPlayable(card, topAttributes, hasDrawn, playableDrawnCard) is null;

    /// <summary>Plain matching, ignoring turn state (used to decide whether a freshly drawn card is playable).</summary>
    public bool Matches(Card card, IReadOnlyDictionary<string, string> topAttributes) =>
        PendingDraw > 0 ? IsStackable(card) : card.IsWild || playRule.Matches(card, topAttributes);

    /// <summary>A card that makes the next player draw and lose their turn can be stacked on a pending penalty.</summary>
    public static bool IsStackable(Card card) =>
        card.Definition.Abilities.Any(a =>
            string.Equals(a.Type, "draw", StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Get("target") ?? "next", "next", StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Get("losesTurn"), "true", StringComparison.OrdinalIgnoreCase));

    public void AddPendingDraw(int count)
    {
        PendingDraw += count;
        Notify(new DrawPenaltyStacked(PendingDraw));
    }

    public int TakePendingDraw()
    {
        var count = PendingDraw;
        PendingDraw = 0;
        return count;
    }

    public void ClearPendingDraw() => PendingDraw = 0;
}
