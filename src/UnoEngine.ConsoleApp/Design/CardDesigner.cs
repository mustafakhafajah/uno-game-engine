using System.Text.RegularExpressions;
using UnoEngine.ConsoleApp.Ui;
using UnoEngine.Core.Cards;
using UnoEngine.Core.Definitions;
using UnoEngine.Rules.Definitions;
using UnoEngine.Rules.Effects;

namespace UnoEngine.ConsoleApp.Design;

/// <summary>Create or edit a game: its rules and its cards (labels, attributes, images, abilities, copies, points).</summary>
internal sealed partial class CardDesigner
{
    private readonly EffectRegistry _effects = EffectRegistry.CreateDefault();
    private readonly List<CardDefinition> _cards = [];
    private readonly List<string> _attributes = [];
    private readonly List<string> _matchAttributes = [];
    private string _name = "My Game";
    private string? _description;
    private int _minPlayers = 2;
    private int _maxPlayers = 6;
    private int _handSize = 7;
    private GameSettings _settings = GameSettings.Default;
    private string? _path;
    private bool _dirty;

    public static void Run()
    {
        Screen.Title("Card designer");
        var designer = new CardDesigner();
        var start = Screen.Menu("Start from", ["A new game", "An existing game (copy or edit it)", "Back"]);
        if (start == 2)
            return;
        if (start == 1)
        {
            if (GameLibrary.Pick("Which game?") is not { } picked)
                return;
            designer.Load(picked.Definition, picked.Path);
        }
        else
        {
            designer.CreateNew();
        }

        designer.EditLoop();
    }

    private void Load(GameDefinition definition, string path)
    {
        (_name, _description, _minPlayers, _maxPlayers, _handSize, _settings) =
            (definition.Name, definition.Description, definition.MinPlayers, definition.MaxPlayers, definition.HandSize, definition.DefaultSettings);
        _attributes.AddRange(definition.Attributes);
        _matchAttributes.AddRange(definition.MatchAttributes);
        _cards.AddRange(definition.Cards);
        _path = path.StartsWith(GameLibrary.PresetsFolder, StringComparison.OrdinalIgnoreCase) ? null : path;
    }

    private void CreateNew()
    {
        _name = Screen.Ask("Game name", _name);
        _description = Screen.AskOptional("Short description");
        EditBasics();
        _dirty = true;
    }

    private void EditLoop()
    {
        while (true)
        {
            Screen.Title($"{_name}: {_cards.Count} kinds of card, {_cards.Sum(c => c.Copies)} cards in the deck");
            var choice = Screen.Menu("What next?",
            [
                "Show the cards",
                "Add a card",
                "Remove a card",
                "Players, hand size and labels (attributes)",
                "Default scoring and house rules",
                "Check the game for problems",
                "Save",
                "Back to the main menu",
            ]);
            switch (choice)
            {
                case 0: ShowCards(); break;
                case 1: AddCard(); break;
                case 2: RemoveCard(); break;
                case 3: EditBasics(); _dirty = true; break;
                case 4: _settings = SettingsEditor.Edit(_settings); _dirty = true; break;
                case 5: Check(); break;
                case 6: Save(); break;
                default:
                    if (!_dirty || Screen.AskYesNo("Leave without saving?", false))
                        return;
                    break;
            }
        }
    }

    private void EditBasics()
    {
        _minPlayers = Screen.AskInt("Fewest players", 2, 20, _minPlayers);
        _maxPlayers = Screen.AskInt("Most players", _minPlayers, 20, Math.Max(_maxPlayers, _minPlayers));
        _handSize = Screen.AskInt("Cards dealt to each player", 1, 30, _handSize);

        Screen.Info("Attributes are the labels a card carries, like colour and symbol in Uno, or animal and habitat.");
        var attributes = Screen.Ask("Attributes, separated by commas", _attributes.Count > 0 ? string.Join(", ", _attributes) : "color, symbol");
        _attributes.Clear();
        _attributes.AddRange(SplitList(attributes));

        Screen.Info("A card can be played on the top card when they share any of the matching attributes.");
        var match = Screen.Ask("Matching attributes", _matchAttributes.Count > 0 ? string.Join(", ", _matchAttributes) : string.Join(", ", _attributes));
        _matchAttributes.Clear();
        _matchAttributes.AddRange(SplitList(match));
    }

    private void ShowCards()
    {
        if (_cards.Count == 0)
        {
            Screen.Dim("No cards yet.");
            return;
        }

        foreach (var card in _cards)
        {
            Console.Write($"  {card.Copies,2} × ");
            Screen.WriteCard(new Card(0, card));
            var details = new List<string> { $"id {card.Id}" };
            if (card.Wild) details.Add("wild");
            if (card.Points > 0) details.Add($"{card.Points} pts");
            if (card.Image is not null) details.Add($"image {card.Image}");
            details.AddRange(card.Abilities.Select(a => a.ToString()));
            Screen.Dim($"      {string.Join(" · ", details)}");
        }
    }

    private void AddCard()
    {
        Screen.Title("New card");
        var label = Screen.Ask("Label (what the card shows, e.g. +2 or Sly Fox)");
        var id = UniqueId(Screen.Ask("Id", Slug(label)));
        var image = Screen.AskOptional("Image file or URL");

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in _attributes)
        {
            var known = _cards.Select(c => c.AttributeOrNull(attribute)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var hint = known.Count > 0 ? $" (used so far: {string.Join(", ", known)})" : "";
            if (Screen.AskOptional($"{attribute}{hint}") is { } value)
                attributes[attribute] = value;
        }

        var wild = Screen.AskYesNo("Wild (can be played on anything)?", false);
        var copies = Screen.AskInt("Copies in the deck", 1, 100, 1);
        var points = Screen.AskInt("Points (for target-score games)", 0, 1000, 0);

        var abilities = new List<AbilityDefinition>();
        while (Screen.AskYesNo(abilities.Count == 0 ? "Give it an ability?" : "Add another ability (they run in order)?", false))
        {
            if (AskAbility() is { } ability)
                abilities.Add(ability);
        }

        var card = new CardDefinition(id, label, attributes, abilities, copies, wild, points, image);
        _cards.Add(card);
        _dirty = true;
        Screen.Success($"Added {copies} × {label}.");
    }

    private AbilityDefinition? AskAbility()
    {
        var catalog = _effects.Catalog.ToList();
        var pick = Screen.Menu("Ability", catalog.Select(e => $"{e.Type}: {e.Description}").ToList());
        var info = catalog[pick];
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in info.Parameters)
        {
            var options = parameter.Options is { Count: > 0 } ? $" ({string.Join("/", parameter.Options)})" : "";
            var fallback = parameter.Default ?? (parameter.Name == "attribute" ? _attributes.FirstOrDefault() : null);
            parameters[parameter.Name] = Screen.Ask($"  {parameter.Description}{options}", fallback);
        }

        var ability = new AbilityDefinition(info.Type, parameters);
        try
        {
            _effects.Create(ability);
            return ability;
        }
        catch (DefinitionException ex)
        {
            Screen.Error(ex.Message);
            return null;
        }
    }

    private void RemoveCard()
    {
        if (_cards.Count == 0)
            return;
        var options = _cards.Select(c => $"{c.Copies} × {c.Label} ({c.Id})").Append("Cancel").ToList();
        var index = Screen.Menu("Remove which card?", options);
        if (index < _cards.Count)
        {
            _cards.RemoveAt(index);
            _dirty = true;
        }
    }

    private GameDefinition Build() =>
        new(_name, _minPlayers, _maxPlayers, _handSize, _attributes.ToList(), _matchAttributes.ToList(), _cards.ToList(), _settings, _description);

    private bool Check()
    {
        var errors = GameDefinitionValidator.Validate(Build(), _effects);
        if (errors.Count == 0)
        {
            Screen.Success("No problems found. The game is ready to play.");
            return true;
        }

        Screen.Error($"{errors.Count} problem(s):");
        foreach (var error in errors)
            Screen.Error($"  - {error}");
        return false;
    }

    private void Save()
    {
        if (!Check() && !Screen.AskYesNo("Save anyway (it can't be played until fixed)?", false))
            return;
        var fallback = _path ?? Path.Combine(GameLibrary.MyGamesFolder, $"{Slug(_name)}.json");
        var path = Screen.Ask("Save to", fallback);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            GameDefinitionLoader.Save(Build(), path);
            (_path, _dirty) = (path, false);
            Screen.Success($"Saved to {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Screen.Error(ex.Message);
        }
    }

    private string UniqueId(string id)
    {
        var unique = id;
        for (var n = 2; _cards.Any(c => string.Equals(c.Id, unique, StringComparison.OrdinalIgnoreCase)); n++)
            unique = $"{id}-{n}";
        return unique;
    }

    private static IEnumerable<string> SplitList(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase);

    private static string Slug(string text)
    {
        var slug = NonWord().Replace(text.ToLowerInvariant().Replace("+", "plus"), "-").Trim('-');
        return slug.Length == 0 ? "card" : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonWord();
}
