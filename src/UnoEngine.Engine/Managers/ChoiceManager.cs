using UnoEngine.Core.Choices;
using UnoEngine.Core.Events;
using UnoEngine.Engine.Mediator;
using UnoEngine.Rules.Effects;

namespace UnoEngine.Engine.Managers;

/// <summary>Holds the decision a card is waiting for, and checks that the right player gives a valid answer.</summary>
public sealed class ChoiceManager(IGameMediator mediator) : GameManager(mediator)
{
    private EffectOutcome.AwaitingChoice? _pending;

    public ChoiceRequest? Pending => _pending?.Request;

    public void Await(EffectOutcome.AwaitingChoice waiting)
    {
        _pending = waiting;
        Notify(new ChoiceRequested(waiting.Request));
    }

    /// <summary>Validates the answer and returns it (in its canonical spelling) with the effect to resume.</summary>
    /// <exception cref="GameRuleException">Nothing is pending, wrong player, or not one of the options.</exception>
    public (string Option, Func<IEffectContext, string, EffectOutcome> Resume) Resolve(int seat, string option)
    {
        var pending = _pending ?? throw new GameRuleException("No choice is waiting.");
        if (pending.Request.Seat != seat)
            throw new GameRuleException("It is not your choice to make.");
        var match = pending.Request.Options.FirstOrDefault(o => string.Equals(o, option.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new GameRuleException($"Choose one of: {string.Join(", ", pending.Request.Options)}.");
        _pending = null;
        Notify(new ChoiceMade(seat, pending.Request.Kind, match));
        return (match, pending.Resume);
    }

    public void Clear() => _pending = null;
}
