using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using UnoEngine.Core.Cards;
using UnoEngine.Core.Definitions;

namespace UnoEngine.Rules.Definitions;

/// <summary>Reads and writes game definitions as JSON (the format card designers edit).</summary>
public static class GameDefinitionLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },

        // Explicit, so the loader also works in hosts where reflection-based JSON is off by default.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static GameDefinition Load(string path) => FromJson(File.ReadAllText(path));

    public static void Save(GameDefinition definition, string path) => File.WriteAllText(path, ToJson(definition));

    /// <exception cref="DefinitionException">The JSON is malformed or misses required parts.</exception>
    public static GameDefinition FromJson(string json)
    {
        DefinitionDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<DefinitionDto>(json, Options)
                  ?? throw new DefinitionException("the file is empty");
        }
        catch (JsonException ex)
        {
            throw new DefinitionException($"the JSON could not be read: {ex.Message}");
        }

        var cards = (dto.Cards ?? []).Select((c, i) => ToCard(c, i)).ToList();
        return new GameDefinition(
            dto.Name ?? throw new DefinitionException("'name' is required"),
            dto.Players?.Min ?? 2,
            dto.Players?.Max ?? 10,
            dto.HandSize ?? 7,
            dto.Attributes ?? [],
            dto.MatchRule?.AnyOf ?? [],
            cards,
            dto.Settings ?? GameSettings.Default,
            dto.Description);
    }

    public static string ToJson(GameDefinition definition)
    {
        var dto = new DefinitionDto
        {
            Name = definition.Name,
            Description = definition.Description,
            Players = new PlayersDto { Min = definition.MinPlayers, Max = definition.MaxPlayers },
            HandSize = definition.HandSize,
            Attributes = definition.Attributes.ToList(),
            MatchRule = new MatchRuleDto { AnyOf = definition.MatchAttributes.ToList() },
            Settings = definition.DefaultSettings,
            Cards = definition.Cards.Select(c => new CardDto
            {
                Id = c.Id,
                Label = c.Label,
                Image = c.Image,
                Attributes = c.Attributes.Count == 0 ? null : new Dictionary<string, string>(c.Attributes),
                Copies = c.Copies,
                Wild = c.Wild ? true : null,
                Points = c.Points == 0 ? null : c.Points,
                Abilities = c.Abilities.Count == 0 ? null : c.Abilities.Select(ToJsonAbility).ToList(),
            }).ToList(),
        };
        return JsonSerializer.Serialize(dto, Options);
    }

    private static CardDefinition ToCard(CardDto dto, int index)
    {
        var id = string.IsNullOrWhiteSpace(dto.Id) ? $"card-{index + 1}" : dto.Id;
        var abilities = (dto.Abilities ?? []).Select(a => ToAbility(a, id)).ToList();
        return new CardDefinition(
            id,
            dto.Label ?? id,
            new Dictionary<string, string>(dto.Attributes ?? [], StringComparer.OrdinalIgnoreCase),
            abilities,
            dto.Copies ?? 1,
            dto.Wild ?? false,
            dto.Points ?? 0,
            dto.Image);
    }

    private static AbilityDefinition ToAbility(Dictionary<string, JsonElement> raw, string cardId)
    {
        var values = raw.ToDictionary(
            p => p.Key,
            p => p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString()!,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => p.Value.GetRawText(),
            },
            StringComparer.OrdinalIgnoreCase);
        if (!values.Remove("type", out var type) || string.IsNullOrWhiteSpace(type))
            throw new DefinitionException($"card '{cardId}': every ability needs a 'type'");
        return new AbilityDefinition(type, values);
    }

    private static Dictionary<string, JsonElement> ToJsonAbility(AbilityDefinition ability)
    {
        var json = new Dictionary<string, JsonElement> { ["type"] = JsonSerializer.SerializeToElement(ability.Type, Options) };
        foreach (var (key, value) in ability.Parameters)
        {
            json[key] = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? JsonSerializer.SerializeToElement(number, Options)
                : bool.TryParse(value, out var flag) ? JsonSerializer.SerializeToElement(flag, Options)
                : JsonSerializer.SerializeToElement(value, Options);
        }

        return json;
    }

    private sealed class DefinitionDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public PlayersDto? Players { get; set; }
        public int? HandSize { get; set; }
        public List<string>? Attributes { get; set; }
        public MatchRuleDto? MatchRule { get; set; }
        public GameSettings? Settings { get; set; }
        public List<CardDto>? Cards { get; set; }
    }

    private sealed class PlayersDto
    {
        public int Min { get; set; } = 2;
        public int Max { get; set; } = 10;
    }

    private sealed class MatchRuleDto
    {
        public List<string>? AnyOf { get; set; }
    }

    private sealed class CardDto
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Image { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
        public int? Copies { get; set; }
        public bool? Wild { get; set; }
        public int? Points { get; set; }

        // Kept as raw JSON so any parameter shape is accepted.
        public List<Dictionary<string, JsonElement>>? Abilities { get; set; }
    }
}
