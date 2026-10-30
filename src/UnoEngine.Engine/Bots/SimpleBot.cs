using UnoEngine.Core.Choices;
using UnoEngine.Engine.Commands;
using UnoEngine.Engine.Mediator;
using UnoEngine.Engine.Views;

namespace UnoEngine.Engine.Bots;

/// <summary>
/// A computer player: plays its first playable card (keeping wilds for last), calls UNO,
/// picks the attribute value it holds most of, and targets the player with the fewest cards.
/// </summary>
public sealed class SimpleBot
{
    /// <summary>The bot's next command, or null when it is not the bot's move.</summary>
    public IGameCommand? Decide(GameMediator game, int seat)
    {
        var view = game.View();

        if (view.Phase == GamePhase.AwaitingChoice && view.PendingChoice is { } choice && choice.Seat == seat)
            return new ChooseCommand(seat, Choose(view, seat, choice));

        if (view.Phase != GamePhase.AwaitingPlay || view.CurrentSeat != seat)
            return null;

        var hand = view.Players[seat].Hand;
        var playable = hand.Where(c => game.CanPlay(seat, c.InstanceId))
            .OrderBy(c => c.IsWild)
            .ToList();
        if (playable.Count > 0)
            return new PlayCardCommand(seat, playable[0].InstanceId, CallUno: hand.Count == 2);

        return view.CurrentPlayerHasDrawn ? new PassCommand(seat) : new DrawCardCommand(seat);
    }

    private static string Choose(GameView view, int seat, ChoiceRequest choice)
    {
        if (choice.Kind == ChoiceKind.Player)
            return choice.Options.OrderBy(o => view.Players[int.Parse(o)].Hand.Count).First();

        var held = view.Players[seat].Hand
            .Select(c => choice.Attribute is null ? null : c.Definition.AttributeOrNull(choice.Attribute))
            .Where(v => v is not null)
            .GroupBy(v => v!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault(v => choice.Options.Contains(v, StringComparer.OrdinalIgnoreCase));
        return held ?? choice.Options[0];
    }
}
