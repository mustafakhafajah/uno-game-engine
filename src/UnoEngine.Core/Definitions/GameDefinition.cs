using UnoEngine.Core.Cards;

namespace UnoEngine.Core.Definitions;

/// <summary>
/// Everything that makes one card game different from another: the cards, the labels they carry,
/// when a card may be played on another, the player range, the hand size and default settings.
/// </summary>
public sealed record GameDefinition(
    string Name,
    int MinPlayers,
    int MaxPlayers,
    int HandSize,
    IReadOnlyList<string> Attributes,
    IReadOnlyList<string> MatchAttributes,
    IReadOnlyList<CardDefinition> Cards,
    GameSettings DefaultSettings,
    string? Description = null)
{
    public int DeckSize => Cards.Sum(c => c.Copies);

    /// <summary>Every value an attribute takes on the cards, e.g. the four colours for "color".</summary>
    public IReadOnlyList<string> ValuesOf(string attribute) =>
        Cards.Select(c => c.AttributeOrNull(attribute))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
