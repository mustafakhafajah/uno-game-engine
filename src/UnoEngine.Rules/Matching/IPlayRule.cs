using UnoEngine.Core.Cards;

namespace UnoEngine.Rules.Matching;

/// <summary>Decides whether a (non-wild) card may be played on the top card (Strategy pattern).</summary>
public interface IPlayRule
{
    bool Matches(Card card, IReadOnlyDictionary<string, string> topAttributes);
}

/// <summary>
/// Playable when the card shares at least one of the listed attributes with the top card.
/// A top card carrying none of them (e.g. a wild with no colour chosen) accepts any card.
/// </summary>
public sealed class MatchAnyAttributeRule(IReadOnlyList<string> attributes) : IPlayRule
{
    public IReadOnlyList<string> Attributes { get; } = attributes;

    public bool Matches(Card card, IReadOnlyDictionary<string, string> topAttributes) =>
        !Attributes.Any(topAttributes.ContainsKey) || Attributes.Any(attribute =>
            card.Definition.AttributeOrNull(attribute) is { } value
            && topAttributes.TryGetValue(attribute, out var top)
            && string.Equals(value, top, StringComparison.OrdinalIgnoreCase));
}
