using System.Globalization;

namespace UnoEngine.Core.Cards;

/// <summary>
/// An ability as written in a game definition: the effect type (e.g. "draw") and its parameters
/// (e.g. target = next, count = 2). The rules layer turns it into a runnable effect.
/// </summary>
public sealed record AbilityDefinition(string Type, IReadOnlyDictionary<string, string> Parameters)
{
    public static AbilityDefinition Of(string type, params (string Key, object Value)[] parameters) =>
        new(type, parameters.ToDictionary(
            p => p.Key,
            p => p.Value is bool b ? (b ? "true" : "false") : Convert.ToString(p.Value, CultureInfo.InvariantCulture)!,
            StringComparer.OrdinalIgnoreCase));

    public string? Get(string key) => Parameters.TryGetValue(key, out var value) ? value : null;

    public override string ToString() =>
        Parameters.Count == 0 ? Type : $"{Type}({string.Join(", ", Parameters.Select(p => $"{p.Key}: {p.Value}"))})";
}
