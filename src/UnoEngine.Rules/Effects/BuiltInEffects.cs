using System.Globalization;
using UnoEngine.Core.Choices;

namespace UnoEngine.Rules.Effects;

public enum DrawTarget
{
    Next,
    All,
    Self,
    Chosen,
}

public enum HandTarget
{
    Next,
    Chosen,
}

public enum RotateDirection
{
    Forward,
    Backward,
}

/// <summary>The next <c>count</c> players lose their turn.</summary>
public sealed class SkipEffect(int count) : ICardEffect
{
    public string Type => "skip";
    public int Count { get; } = count;

    public EffectOutcome Apply(IEffectContext context)
    {
        context.Skip(Count);
        return EffectOutcome.Done;
    }
}

/// <summary>The direction of play flips.</summary>
public sealed class ReverseEffect : ICardEffect
{
    public string Type => "reverse";

    public EffectOutcome Apply(IEffectContext context)
    {
        context.Reverse();
        return EffectOutcome.Done;
    }
}

/// <summary>Someone draws cards: the next player, everyone else, yourself, or a player you choose.</summary>
public sealed class DrawEffect(DrawTarget target, int count, bool losesTurn) : ICardEffect
{
    public string Type => "draw";
    public DrawTarget Target { get; } = target;
    public int Count { get; } = count;
    public bool LosesTurn { get; } = losesTurn;

    public EffectOutcome Apply(IEffectContext context)
    {
        switch (Target)
        {
            case DrawTarget.Next:
                context.PenalizeNextPlayer(Count, LosesTurn);
                return EffectOutcome.Done;
            case DrawTarget.All:
                foreach (var seat in context.OtherSeats())
                    context.DrawCards(seat, Count);
                return EffectOutcome.Done;
            case DrawTarget.Self:
                context.DrawCards(context.CurrentSeat, Count);
                return EffectOutcome.Done;
            default:
                return EffectOutcome.Ask(
                    ChoosePlayer(context, $"Choose a player to draw {Count}"),
                    (ctx, option) =>
                    {
                        ctx.DrawCards(ParseSeat(option), Count);
                        return EffectOutcome.Done;
                    });
        }
    }

    internal static ChoiceRequest ChoosePlayer(IEffectContext context, string prompt) =>
        new(context.CurrentSeat, ChoiceKind.Player, prompt,
            context.OtherSeats().Select(s => s.ToString(CultureInfo.InvariantCulture)).ToList());

    internal static int ParseSeat(string option) => int.Parse(option, CultureInfo.InvariantCulture);
}

/// <summary>The player picks a value for an attribute, e.g. the colour after a Wild.</summary>
public sealed class ChooseAttributeEffect(string attribute) : ICardEffect
{
    public string Type => "chooseAttribute";
    public string Attribute { get; } = attribute;

    public EffectOutcome Apply(IEffectContext context)
    {
        var request = new ChoiceRequest(
            context.CurrentSeat, ChoiceKind.AttributeValue, $"Choose a {Attribute}", context.ValuesOf(Attribute), Attribute);
        return EffectOutcome.Ask(request, (ctx, option) =>
        {
            ctx.SetActiveAttribute(Attribute, option);
            return EffectOutcome.Done;
        });
    }
}

/// <summary>The same player takes another turn.</summary>
public sealed class PlayAgainEffect : ICardEffect
{
    public string Type => "playAgain";

    public EffectOutcome Apply(IEffectContext context)
    {
        context.PlayAgain();
        return EffectOutcome.Done;
    }
}

/// <summary>Swap hands with the next player or a player you choose.</summary>
public sealed class SwapHandsEffect(HandTarget target) : ICardEffect
{
    public string Type => "swapHands";
    public HandTarget Target { get; } = target;

    public EffectOutcome Apply(IEffectContext context)
    {
        if (Target == HandTarget.Next)
        {
            context.SwapHands(context.CurrentSeat, context.NextSeat());
            return EffectOutcome.Done;
        }

        return EffectOutcome.Ask(
            DrawEffect.ChoosePlayer(context, "Choose a player to swap hands with"),
            (ctx, option) =>
            {
                ctx.SwapHands(ctx.CurrentSeat, DrawEffect.ParseSeat(option));
                return EffectOutcome.Done;
            });
    }
}

/// <summary>Every player passes their hand along, forward (direction of play) or backward.</summary>
public sealed class RotateHandsEffect(RotateDirection direction) : ICardEffect
{
    public string Type => "rotateHands";
    public RotateDirection Direction { get; } = direction;

    public EffectOutcome Apply(IEffectContext context)
    {
        context.RotateHands(Direction == RotateDirection.Forward);
        return EffectOutcome.Done;
    }
}

/// <summary>The player also discards every card in their hand that shares the played card's value for an attribute.</summary>
public sealed class DiscardAllEffect(string attribute) : ICardEffect
{
    public string Type => "discardAll";
    public string Attribute { get; } = attribute;

    public EffectOutcome Apply(IEffectContext context)
    {
        if (context.ActiveValue(Attribute) is { } value)
            context.DiscardMatching(context.CurrentSeat, Attribute, value);
        return EffectOutcome.Done;
    }
}
