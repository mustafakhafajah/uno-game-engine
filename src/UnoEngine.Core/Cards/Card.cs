namespace UnoEngine.Core.Cards;

/// <summary>A physical card in play: one copy of a <see cref="CardDefinition"/> with its own id.</summary>
public sealed class Card(int instanceId, CardDefinition definition)
{
    public int InstanceId { get; } = instanceId;
    public CardDefinition Definition { get; } = definition;

    public string Label => Definition.Label;
    public bool IsWild => Definition.Wild;
    public IReadOnlyDictionary<string, string> Attributes => Definition.Attributes;

    public override string ToString() =>
        Attributes.Count == 0 ? Label : $"{Label} ({string.Join(", ", Attributes.Values)})";
}
