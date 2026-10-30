using UnoEngine.Core.Definitions;
using UnoEngine.Rules.Effects;

namespace UnoEngine.Rules.Definitions;

/// <summary>Finds every problem in a game definition, each named with the card it belongs to.</summary>
public static class GameDefinitionValidator
{
    public static IReadOnlyList<string> Validate(GameDefinition definition, EffectRegistry effects)
    {
        var errors = new List<string>();
        var attributes = new HashSet<string>(definition.Attributes, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(definition.Name))
            errors.Add("the game needs a name");
        if (definition.MinPlayers < 2)
            errors.Add("at least 2 players are needed");
        if (definition.MaxPlayers < definition.MinPlayers)
            errors.Add($"the maximum players ({definition.MaxPlayers}) is lower than the minimum ({definition.MinPlayers})");
        if (definition.HandSize < 1)
            errors.Add("the hand size must be at least 1");
        if (attributes.Count == 0)
            errors.Add("declare at least one attribute (a label cards carry, e.g. 'color')");
        if (definition.MatchAttributes.Count == 0)
            errors.Add("the match rule needs at least one attribute");
        foreach (var match in definition.MatchAttributes.Where(m => !attributes.Contains(m)))
            errors.Add($"the match rule uses '{match}', which is not a declared attribute");

        if (definition.Cards.Count == 0)
            errors.Add("the deck has no cards");
        foreach (var duplicate in definition.Cards.GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"the card id '{duplicate.Key}' is used {duplicate.Count()} times");

        foreach (var card in definition.Cards)
        {
            var where = $"card '{card.Id}'";
            if (string.IsNullOrWhiteSpace(card.Label))
                errors.Add($"{where}: needs a label");
            if (card.Copies < 1)
                errors.Add($"{where}: copies must be at least 1");
            if (card.Points < 0)
                errors.Add($"{where}: points cannot be negative");
            foreach (var attribute in card.Attributes.Keys.Where(a => !attributes.Contains(a)))
                errors.Add($"{where}: '{attribute}' is not a declared attribute");
            if (!card.Wild && !definition.MatchAttributes.Any(m => card.AttributeOrNull(m) is not null))
                errors.Add($"{where}: has none of the match attributes and is not wild, so it could never be played");

            foreach (var ability in card.Abilities)
            {
                try
                {
                    var effect = effects.Create(ability);
                    if (effect is ChooseAttributeEffect choose && !attributes.Contains(choose.Attribute))
                        errors.Add($"{where}: chooseAttribute uses '{choose.Attribute}', which is not a declared attribute");
                    if (effect is DiscardAllEffect discard && !attributes.Contains(discard.Attribute))
                        errors.Add($"{where}: discardAll uses '{discard.Attribute}', which is not a declared attribute");
                }
                catch (DefinitionException ex)
                {
                    errors.Add($"{where}: {ex.Message}");
                }
            }
        }

        var needed = definition.HandSize * definition.MinPlayers + 1;
        if (definition.Cards.Count > 0 && definition.DeckSize < needed)
            errors.Add($"the deck has {definition.DeckSize} cards, but {definition.MinPlayers} players with {definition.HandSize} cards each need at least {needed}");

        var settings = definition.DefaultSettings;
        if (settings.TargetScore < 1)
            errors.Add("the target score must be at least 1");
        if (settings.UnoPenalty < 0)
            errors.Add("the UNO penalty cannot be negative");

        return errors;
    }

    /// <exception cref="DefinitionException">When anything is wrong, with every error listed.</exception>
    public static void EnsureValid(GameDefinition definition, EffectRegistry effects)
    {
        var errors = Validate(definition, effects);
        if (errors.Count > 0)
            throw new DefinitionException(errors);
    }
}
