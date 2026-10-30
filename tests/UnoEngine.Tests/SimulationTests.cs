using UnoEngine.Core.Definitions;
using UnoEngine.Core.Randomness;
using UnoEngine.Engine.Bots;
using UnoEngine.Engine.Commands;
using UnoEngine.Engine.Mediator;
using UnoEngine.Rules.Presets;
using UnoEngine.Tests.Support;

namespace UnoEngine.Tests;

/// <summary>Whole games played by bots: they must finish, never break a rule, and never lose a card.</summary>
public class SimulationTests
{
    private static GameMediator PlayOut(GameDefinition definition, int players, GameSettings settings, int seed, int maxCommands = 20_000)
    {
        var names = Enumerable.Range(0, players).Select(i => $"Bot {i}").ToList();
        var game = new GameMediator(definition, names, settings, random: new SeededRandom(seed));
        var bot = new SimpleBot();
        game.Start().ShouldSucceed();

        for (var step = 0; step < maxCommands && game.Phase != GamePhase.GameOver; step++)
        {
            if (game.Phase == GamePhase.RoundOver)
            {
                game.StartNextRound().ShouldSucceed();
                continue;
            }

            var view = game.View();
            var seat = view.PendingChoice?.Seat ?? view.CurrentSeat;
            var command = bot.Decide(game, seat).ShouldNotBeNull();
            game.Execute(command).ShouldSucceed();
            game.ShouldConserveCards();
        }

        game.Phase.ShouldBe(GamePhase.GameOver, $"seed {seed} did not finish");
        return game;
    }

    public static TheoryData<int, int, bool, DrawStyle> ClassicGames => new()
    {
        { 1, 2, false, DrawStyle.OneCard },
        { 2, 3, true, DrawStyle.OneCard },
        { 3, 4, false, DrawStyle.UntilPlayable },
        { 4, 5, true, DrawStyle.UntilPlayable },
        { 5, 6, false, DrawStyle.OneCard },
        { 6, 10, true, DrawStyle.OneCard },
    };

    [Theory]
    [MemberData(nameof(ClassicGames))]
    public void Classic_games_between_bots_always_finish(int seed, int players, bool stacking, DrawStyle drawStyle)
    {
        var settings = GameSettings.Default with { StackDrawCards = stacking, DrawStyle = drawStyle };

        var game = PlayOut(ClassicUno.Create(), players, settings, seed);

        game.View().Players[game.WinnerSeat!.Value].Hand.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    public void Classic_played_to_a_target_score_runs_several_rounds(int seed)
    {
        var settings = GameSettings.Default with { Scoring = ScoringMode.TargetScore, TargetScore = 300 };

        var game = PlayOut(ClassicUno.Create(), 4, settings, seed);

        game.View().Players[game.WinnerSeat!.Value].Score.ShouldBeGreaterThanOrEqualTo(300);
    }

    [Theory]
    [InlineData(21, 3)]
    [InlineData(22, 4)]
    [InlineData(23, 6)]
    public void Animal_party_games_between_bots_always_finish(int seed, int players)
    {
        var party = Rig.LoadPreset("animal-party.json");

        PlayOut(party, players, party.DefaultSettings, seed);
    }

    [Fact]
    public void Replaying_the_command_history_with_the_same_seed_recreates_the_game()
    {
        var original = PlayOut(ClassicUno.Create(), 4, GameSettings.Default, seed: 42);

        var replay = new GameMediator(ClassicUno.Create(), ["Bot 0", "Bot 1", "Bot 2", "Bot 3"], GameSettings.Default, random: new SeededRandom(42));
        foreach (IGameCommand command in original.History)
            replay.Execute(command).ShouldSucceed();

        replay.Events.Select(e => e.ToString()).ShouldBe(original.Events.Select(e => e.ToString()));
        replay.WinnerSeat.ShouldBe(original.WinnerSeat);
    }
}
