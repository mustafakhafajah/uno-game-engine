using UnoEngine.Core.Cards;
using UnoEngine.Core.Definitions;
using UnoEngine.Engine.Mediator;

namespace UnoEngine.Engine.Managers;

public sealed record RoundResult(int WinnerSeat, int Points, bool GameOver);

/// <summary>Counts rounds and points, and decides when the game is over (first out, or a target score).</summary>
public sealed class ScoreManager(IGameMediator mediator, GameSettings settings) : GameManager(mediator)
{
    public int Round { get; private set; }

    public void StartRound() => Round++;

    /// <summary>
    /// Scores a finished round. With scoring off the round winner wins the game; with a target score
    /// they earn the points still held by everyone else, and win once they reach the target.
    /// </summary>
    public RoundResult FinishRound(int winnerSeat, int winnerScoreBefore, IEnumerable<IReadOnlyList<Card>> otherHands)
    {
        if (settings.Scoring == ScoringMode.FirstOutWins)
            return new RoundResult(winnerSeat, 0, GameOver: true);

        var points = otherHands.Sum(hand => hand.Sum(card => card.Definition.Points));
        return new RoundResult(winnerSeat, points, GameOver: winnerScoreBefore + points >= settings.TargetScore);
    }
}
