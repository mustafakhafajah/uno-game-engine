using UnoEngine.Core.Cards;
using UnoEngine.Rules.Definitions;

namespace UnoEngine.Rules.Effects;

/// <summary>Describes an effect for card designers: what it does and which parameters it takes.</summary>
public sealed record EffectInfo(string Type, string Description, IReadOnlyList<EffectParameterInfo> Parameters);

public sealed record EffectParameterInfo(string Name, string Description, string? Default = null, IReadOnlyList<string>? Options = null);

/// <summary>
/// Turns abilities written in a game definition into runnable effects (Factory / Registry).
/// Register your own effect type to add a new building block without touching the engine.
/// </summary>
public sealed class EffectRegistry
{
    private readonly Dictionary<string, Func<AbilityDefinition, ICardEffect>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EffectInfo> _infos = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<EffectInfo> Catalog => _infos.Values;

    public EffectRegistry Register(string type, Func<AbilityDefinition, ICardEffect> factory, EffectInfo? info = null)
    {
        _factories[type] = factory;
        _infos[type] = info ?? new EffectInfo(type, "Custom effect", []);
        return this;
    }

    public bool IsKnown(string type) => _factories.ContainsKey(type);

    /// <exception cref="DefinitionException">Unknown type or bad parameters.</exception>
    public ICardEffect Create(AbilityDefinition ability)
    {
        if (!_factories.TryGetValue(ability.Type, out var factory))
            throw new DefinitionException(
                $"unknown ability '{ability.Type}' (known: {string.Join(", ", _factories.Keys.Order())})");
        return factory(ability);
    }

    public static EffectRegistry CreateDefault()
    {
        static IReadOnlyList<string> Names<T>() where T : struct, Enum =>
            Enum.GetNames<T>().Select(AbilityParameters.Camel).ToList();

        return new EffectRegistry()
            .Register("skip",
                a => new SkipEffect(a.Int("count", 1, min: 1, max: 20)),
                new EffectInfo("skip", "The next player(s) lose their turn.",
                    [new("count", "How many players are skipped", "1")]))
            .Register("reverse",
                _ => new ReverseEffect(),
                new EffectInfo("reverse", "The direction of play flips.", []))
            .Register("draw",
                a => new DrawEffect(a.Choice<DrawTarget>("target", DrawTarget.Next), a.Int("count", min: 1, max: 50), a.Bool("losesTurn", false)),
                new EffectInfo("draw", "Someone draws cards.",
                [
                    new("target", "Who draws", "next", Names<DrawTarget>()),
                    new("count", "How many cards"),
                    new("losesTurn", "The next player also loses their turn (needed for stacking)", "false", ["true", "false"]),
                ]))
            .Register("chooseAttribute",
                a => new ChooseAttributeEffect(a.Text("attribute")),
                new EffectInfo("chooseAttribute", "The player picks a value for an attribute, e.g. the colour after a Wild.",
                    [new("attribute", "Which attribute is chosen")]))
            .Register("playAgain",
                _ => new PlayAgainEffect(),
                new EffectInfo("playAgain", "The same player takes another turn.", []))
            .Register("swapHands",
                a => new SwapHandsEffect(a.Choice<HandTarget>("target", HandTarget.Chosen)),
                new EffectInfo("swapHands", "Swap hands with another player.",
                    [new("target", "Who you swap with", "chosen", Names<HandTarget>())]))
            .Register("rotateHands",
                a => new RotateHandsEffect(a.Choice<RotateDirection>("direction", RotateDirection.Forward)),
                new EffectInfo("rotateHands", "Every player passes their hand along.",
                    [new("direction", "Which way hands move", "forward", Names<RotateDirection>())]))
            .Register("discardAll",
                a => new DiscardAllEffect(a.Text("attribute")),
                new EffectInfo("discardAll", "Also discard every card in your hand with the same value, e.g. all your red cards.",
                    [new("attribute", "Which attribute must match")]));
    }
}
