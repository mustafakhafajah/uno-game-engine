using System.Globalization;
using UnoEngine.Core.Cards;
using UnoEngine.Rules.Definitions;

namespace UnoEngine.Rules.Effects;

/// <summary>Reads an ability's parameters with clear errors, for effect factories (built-in or custom).</summary>
public static class AbilityParameters
{
    public static int Int(this AbilityDefinition ability, string key, int? fallback = null, int min = int.MinValue, int max = int.MaxValue)
    {
        var raw = ability.Get(key);
        if (raw is null)
            return fallback ?? throw Missing(ability, key);
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new DefinitionException($"ability '{ability.Type}': '{key}' must be a whole number, not '{raw}'");
        if (value < min || value > max)
            throw new DefinitionException($"ability '{ability.Type}': '{key}' must be between {min} and {max}, not {value}");
        return value;
    }

    public static bool Bool(this AbilityDefinition ability, string key, bool fallback)
    {
        var raw = ability.Get(key);
        if (raw is null)
            return fallback;
        return bool.TryParse(raw, out var value)
            ? value
            : throw new DefinitionException($"ability '{ability.Type}': '{key}' must be true or false, not '{raw}'");
    }

    public static string Text(this AbilityDefinition ability, string key, string? fallback = null)
    {
        var raw = ability.Get(key);
        return string.IsNullOrWhiteSpace(raw) ? fallback ?? throw Missing(ability, key) : raw;
    }

    public static T Choice<T>(this AbilityDefinition ability, string key, T? fallback = null) where T : struct, Enum
    {
        var raw = ability.Get(key);
        if (raw is null)
            return fallback ?? throw Missing(ability, key);
        return Enum.TryParse<T>(raw, ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : throw new DefinitionException(
                $"ability '{ability.Type}': '{key}' must be one of {string.Join(", ", Enum.GetNames<T>().Select(Camel))}, not '{raw}'");
    }

    internal static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static DefinitionException Missing(AbilityDefinition ability, string key) =>
        new($"ability '{ability.Type}': '{key}' is required");
}
