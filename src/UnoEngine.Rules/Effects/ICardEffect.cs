using UnoEngine.Core.Choices;

namespace UnoEngine.Rules.Effects;

/// <summary>
/// One building block of a card's abilities (Strategy pattern). A card runs its effects in the
/// order they are listed; an effect that needs a player's decision returns <see cref="EffectOutcome.Ask"/>.
/// </summary>
public interface ICardEffect
{
    string Type { get; }
    EffectOutcome Apply(IEffectContext context);
}

public abstract record EffectOutcome
{
    public static EffectOutcome Done { get; } = new Completed();

    /// <summary>Pause the card until the player answers; <paramref name="resume"/> runs with their answer.</summary>
    public static EffectOutcome Ask(ChoiceRequest request, Func<IEffectContext, string, EffectOutcome> resume) =>
        new AwaitingChoice(request, resume);

    public sealed record Completed : EffectOutcome;

    public sealed record AwaitingChoice(ChoiceRequest Request, Func<IEffectContext, string, EffectOutcome> Resume) : EffectOutcome;
}
