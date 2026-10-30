namespace UnoEngine.Core.Cards;

/// <summary>
/// One kind of card in a game definition: how it looks (label, image), the labels it carries
/// (attributes such as colour or symbol), what it does (abilities) and how many copies are in the deck.
/// </summary>
public sealed record CardDefinition(
    string Id,
    string Label,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<AbilityDefinition> Abilities,
    int Copies = 1,
    bool Wild = false,
    int Points = 0,
    string? Image = null)
{
    public string? AttributeOrNull(string attribute) =>
        Attributes.TryGetValue(attribute, out var value) ? value : null;
}
