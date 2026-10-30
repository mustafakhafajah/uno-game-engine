using UnoEngine.Core.Cards;
using UnoEngine.Engine.Mediator;
using UnoEngine.Rules.Effects;

namespace UnoEngine.Engine.Managers;

/// <summary>Runs a played card's abilities in order, stopping when one needs a player's decision.</summary>
public sealed class EffectManager(IGameMediator mediator, EffectRegistry registry) : GameManager(mediator)
{
    private readonly Dictionary<CardDefinition, IReadOnlyList<ICardEffect>> _cache = [];
    private readonly Queue<ICardEffect> _remaining = new();

    public bool IsRunning => _remaining.Count > 0;

    /// <summary>Queues the card's effects; call <see cref="RunUntilChoice"/> next.</summary>
    public void Load(Card card)
    {
        _remaining.Clear();
        foreach (var effect in EffectsOf(card.Definition))
            _remaining.Enqueue(effect);
    }

    /// <summary>Runs queued effects. Returns the pending choice, or null when the card is finished.</summary>
    public EffectOutcome.AwaitingChoice? RunUntilChoice(IEffectContext context)
    {
        while (_remaining.TryDequeue(out var effect))
        {
            if (effect.Apply(context) is EffectOutcome.AwaitingChoice waiting)
                return waiting;
        }

        return null;
    }

    /// <summary>Feeds a player's answer back to the paused effect, then carries on with the rest.</summary>
    public EffectOutcome.AwaitingChoice? Continue(IEffectContext context, Func<IEffectContext, string, EffectOutcome> resume, string option) =>
        resume(context, option) as EffectOutcome.AwaitingChoice ?? RunUntilChoice(context);

    public void Clear() => _remaining.Clear();

    private IReadOnlyList<ICardEffect> EffectsOf(CardDefinition definition)
    {
        if (!_cache.TryGetValue(definition, out var effects))
            _cache[definition] = effects = definition.Abilities.Select(registry.Create).ToList();
        return effects;
    }
}
