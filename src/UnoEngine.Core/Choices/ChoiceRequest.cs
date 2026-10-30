namespace UnoEngine.Core.Choices;

public enum ChoiceKind
{
    /// <summary>Pick a value for an attribute, e.g. the colour after a Wild.</summary>
    AttributeValue,

    /// <summary>Pick another player; options are seat numbers.</summary>
    Player,
}

/// <summary>A decision a card ability needs from a player before the game can go on.</summary>
public sealed record ChoiceRequest(
    int Seat,
    ChoiceKind Kind,
    string Prompt,
    IReadOnlyList<string> Options,
    string? Attribute = null);
