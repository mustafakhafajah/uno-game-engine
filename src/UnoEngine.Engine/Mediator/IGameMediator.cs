using UnoEngine.Core.Events;

namespace UnoEngine.Engine.Mediator;

/// <summary>
/// The only channel managers have to the rest of the game (Mediator pattern).
/// Managers never call each other; they report what happened, and the mediator decides what follows.
/// </summary>
public interface IGameMediator
{
    void Notify(GameEvent gameEvent);
}

/// <summary>Base for the managers the mediator coordinates.</summary>
public abstract class GameManager(IGameMediator mediator)
{
    protected void Notify(GameEvent gameEvent) => mediator.Notify(gameEvent);
}

/// <summary>A player tried something the rules don't allow; nothing in the game has changed.</summary>
public sealed class GameRuleException(string message) : Exception(message);
