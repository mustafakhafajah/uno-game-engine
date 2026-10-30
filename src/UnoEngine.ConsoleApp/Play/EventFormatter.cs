using UnoEngine.ConsoleApp.Ui;
using UnoEngine.Core.Choices;
using UnoEngine.Core.Events;
using UnoEngine.Engine.Mediator;

namespace UnoEngine.ConsoleApp.Play;

/// <summary>Prints game events as plain sentences (the console's Observer of the mediator).</summary>
internal sealed class EventFormatter(GameMediator game)
{
    private string Name(int seat) => game.View().Players[seat].Name;

    public void Print(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case RoundStarted e:
                Screen.Title($"Round {e.Round}: {Name(e.StartingSeat)} starts");
                break;
            case FirstCardFlipped e:
                Console.Write("  First card: ");
                Screen.WriteCard(e.Card);
                Console.WriteLine();
                break;
            case CardPlayed e:
                Console.Write($"  {Name(e.Seat)} plays ");
                Screen.WriteCard(e.Card);
                Console.WriteLine();
                break;
            case CardsDrawn { Reason: DrawReason.Deal }:
                break;
            case CardsDrawn e:
                Screen.Dim($"  {Name(e.Seat)} draws {Cards(e.Count)}{Reason(e.Reason)}");
                break;
            case TurnPassed e:
                Screen.Dim($"  {Name(e.Seat)} passes");
                break;
            case PlayerSkipped e:
                Screen.Info($"  {Name(e.Seat)} is skipped");
                break;
            case DirectionReversed e:
                Screen.Info($"  Direction reversed ({(e.Clockwise ? "clockwise" : "counter-clockwise")})");
                break;
            case PlayAgainGranted e:
                Screen.Info($"  {Name(e.Seat)} plays again");
                break;
            case DrawPenaltyStacked e:
                Screen.Info($"  Penalty now {Cards(e.Total)}: stack a draw card or take them");
                break;
            case ChoiceMade { Kind: ChoiceKind.Player } e:
                Screen.Info($"  {Name(e.Seat)} picks {Name(int.Parse(e.Option))}");
                break;
            case AttributeChosen e:
                Screen.Info($"  {Name(e.Seat)} chooses {e.Attribute}: {e.Value}");
                break;
            case HandsSwapped e:
                Screen.Info($"  {Name(e.SeatA)} and {Name(e.SeatB)} swap hands");
                break;
            case HandsRotated e:
                Screen.Info($"  Everyone passes their hand {(e.Forward ? "forward" : "backward")}");
                break;
            case CardsDiscarded e:
                Screen.Info($"  {Name(e.Seat)} also discards {Cards(e.Count)}");
                break;
            case UnoCalled e:
                Screen.WriteLine($"  {Name(e.Seat)}: UNO!", ConsoleColor.Yellow);
                break;
            case UnoCaught e:
                Screen.WriteLine($"  {Name(e.CatcherSeat)} caught {Name(e.Seat)} without UNO: +{e.Penalty}", ConsoleColor.Yellow);
                break;
            case DeckReshuffled e:
                Screen.Dim($"  The discard pile is shuffled into a new draw pile ({e.CardCount} cards)");
                break;
            case RoundWon e:
                Screen.Success(e.Points > 0
                    ? $"  {Name(e.Seat)} wins the round and scores {e.Points}. Scores: {string.Join(", ", e.Scores.Select(s => $"{Name(s.Key)} {s.Value}"))}"
                    : $"  {Name(e.Seat)} is out of cards!");
                break;
            case GameWon e:
                Screen.Title($"{Name(e.Seat)} wins the game!");
                break;
        }
    }

    private static string Cards(int count) => count == 1 ? "1 card" : $"{count} cards";

    private static string Reason(DrawReason reason) => reason switch
    {
        DrawReason.CardEffect => " (card effect)",
        DrawReason.StackedPenalty => " (penalty)",
        DrawReason.UnoPenalty => " (UNO penalty)",
        _ => "",
    };
}
