using UnoEngine.Core.Cards;
using UnoEngine.Core.Definitions;

namespace UnoEngine.Rules.Definitions;

/// <summary>Fluent builder for creating a game definition in code (Builder pattern).</summary>
public sealed class GameDefinitionBuilder(string name)
{
    private readonly List<CardDefinition> _cards = [];
    private readonly List<string> _attributes = [];
    private readonly List<string> _matchAttributes = [];
    private int _minPlayers = 2;
    private int _maxPlayers = 10;
    private int _handSize = 7;
    private string? _description;
    private GameSettings _settings = GameSettings.Default;

    public GameDefinitionBuilder Description(string description)
    {
        _description = description;
        return this;
    }

    public GameDefinitionBuilder Players(int min, int max)
    {
        (_minPlayers, _maxPlayers) = (min, max);
        return this;
    }

    public GameDefinitionBuilder HandSize(int size)
    {
        _handSize = size;
        return this;
    }

    public GameDefinitionBuilder Attributes(params string[] attributes)
    {
        _attributes.AddRange(attributes);
        return this;
    }

    /// <summary>A card can be played when it shares any of these attributes with the top card.</summary>
    public GameDefinitionBuilder MatchOnAny(params string[] attributes)
    {
        _matchAttributes.AddRange(attributes);
        return this;
    }

    public GameDefinitionBuilder Settings(Func<GameSettings, GameSettings> change)
    {
        _settings = change(_settings);
        return this;
    }

    public GameDefinitionBuilder Card(string id, string label, Action<CardBuilder>? configure = null)
    {
        var card = new CardBuilder(id, label);
        configure?.Invoke(card);
        _cards.Add(card.Build());
        return this;
    }

    public GameDefinition Build() =>
        new(name, _minPlayers, _maxPlayers, _handSize, _attributes.ToList(), _matchAttributes.ToList(), _cards.ToList(), _settings, _description);
}

public sealed class CardBuilder(string id, string label)
{
    private readonly Dictionary<string, string> _attributes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AbilityDefinition> _abilities = [];
    private int _copies = 1;
    private bool _wild;
    private int _points;
    private string? _image;

    public CardBuilder With(string attribute, string value)
    {
        _attributes[attribute] = value;
        return this;
    }

    public CardBuilder Copies(int copies)
    {
        _copies = copies;
        return this;
    }

    public CardBuilder Wild()
    {
        _wild = true;
        return this;
    }

    public CardBuilder Points(int points)
    {
        _points = points;
        return this;
    }

    public CardBuilder Image(string path)
    {
        _image = path;
        return this;
    }

    public CardBuilder Ability(string type, params (string Key, object Value)[] parameters)
    {
        _abilities.Add(AbilityDefinition.Of(type, parameters));
        return this;
    }

    internal CardDefinition Build() =>
        new(id, label, new Dictionary<string, string>(_attributes, StringComparer.OrdinalIgnoreCase), _abilities.ToList(), _copies, _wild, _points, _image);
}
