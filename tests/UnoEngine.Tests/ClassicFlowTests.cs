using UnoEngine.Core.Choices;
using UnoEngine.Core.Definitions;
using UnoEngine.Core.Events;
using UnoEngine.Core.Randomness;
using UnoEngine.Engine.Mediator;
using UnoEngine.Rules.Definitions;
using UnoEngine.Tests.Support;

namespace UnoEngine.Tests;

/// <summary>The classic Uno workflow through the mediator: play, draw, pass, choices, UNO, winning.</summary>
public class ClassicFlowTests
{
    [Fact]
    public void A_matching_card_is_played_and_the_turn_moves_on()
    {
        var game = Rig.Classic();
        Rig.Hands(game, ["red-5", "blue-7"], ["red-1"], ["blue-1"]);
        Rig.Top(game, "red-3");

        game.Play(0, Rig.Card(game, 0, "blue-7")).ShouldFailWith("does not match");
        game.Play(1, Rig.Card(game, 1, "red-1")).ShouldFailWith("It is P0's turn");
        game.Play(0, Rig.Card(game, 0, "red-5")).ShouldSucceed();

        game.View().CurrentSeat.ShouldBe(1);
        game.View().TopCard!.Definition.Id.ShouldBe("red-5");
        game.ShouldConserveCards();
    }

    [Fact]
    public void Skip_jumps_over_the_next_player()
    {
        var game = Rig.Classic();
        Rig.Hands(game, ["red-skip", "red-1"], ["red-2"], ["red-3"]);
        Rig.Top(game, "red-4");

        game.Play(0, Rig.Card(game, 0, "red-skip")).ShouldSucceed();

        game.View().CurrentSeat.ShouldBe(2);
        game.Events.ShouldContain(new PlayerSkipped(1));
    }

    [Fact]
    public void Reverse_flips_the_direction_and_acts_as_skip_with_two_players()
    {
        var three = Rig.Classic(3);
        Rig.Hands(three, ["red-reverse", "red-1"], ["red-2"], ["red-3", "red-6"]);
        Rig.Top(three, "red-4");
        three.Play(0, Rig.Card(three, 0, "red-reverse")).ShouldSucceed();
        three.View().CurrentSeat.ShouldBe(2);
        three.Play(2, Rig.Card(three, 2, "red-3")).ShouldSucceed();
        three.View().CurrentSeat.ShouldBe(1);

        var two = Rig.Classic(2);
        Rig.Hands(two, ["red-reverse", "red-1"], ["red-2"]);
        Rig.Top(two, "red-4");
        two.Play(0, Rig.Card(two, 0, "red-reverse")).ShouldSucceed();
        two.View().CurrentSeat.ShouldBe(0);
    }

    [Fact]
    public void Draw_two_makes_the_next_player_draw_and_lose_their_turn()
    {
        var game = Rig.Classic();
        Rig.Hands(game, ["red-draw2", "red-1"], ["blue-2"], ["red-3"]);
        Rig.Top(game, "red-4");

        game.Play(0, Rig.Card(game, 0, "red-draw2")).ShouldSucceed();

        Rig.HandSize(game, 1).ShouldBe(3);
        game.View().CurrentSeat.ShouldBe(2);
        game.ShouldConserveCards();
    }

    [Fact]
    public void With_stacking_a_draw_card_passes_the_growing_penalty_on()
    {
        var game = Rig.Classic(3, s => s with { StackDrawCards = true });
        Rig.Hands(game, ["red-draw2", "red-1"], ["blue-draw2", "blue-5"], ["red-3"]);
        Rig.Top(game, "red-4");

        game.Play(0, Rig.Card(game, 0, "red-draw2")).ShouldSucceed();
        game.View().PendingDraw.ShouldBe(2);
        game.Play(1, Rig.Card(game, 1, "blue-draw2")).ShouldSucceed();
        game.View().PendingDraw.ShouldBe(4);

        game.Play(2, Rig.Card(game, 2, "red-3")).ShouldFailWith("draw 4");
        game.Draw(2).ShouldSucceed();

        Rig.HandSize(game, 2).ShouldBe(5);
        game.View().PendingDraw.ShouldBe(0);
        game.View().CurrentSeat.ShouldBe(0);
        game.ShouldConserveCards();
    }

    [Fact]
    public void Wild_draw_four_waits_for_a_colour_then_penalises_the_next_player()
    {
        var game = Rig.Classic();
        Rig.Hands(game, ["wild-draw4", "red-1"], ["green-2"], ["blue-1"]);
        Rig.Top(game, "red-4");

        game.Play(0, Rig.Card(game, 0, "wild-draw4")).ShouldSucceed();

        var view = game.View();
        view.Phase.ShouldBe(GamePhase.AwaitingChoice);
        view.PendingChoice.ShouldNotBeNull().Kind.ShouldBe(ChoiceKind.AttributeValue);
        view.PendingChoice.Options.ShouldBe(["red", "yellow", "green", "blue"], ignoreOrder: true);
        game.Play(1, Rig.Card(game, 1, "green-2")).ShouldFailWith("Waiting for P0 to choose");
        game.Choose(1, "blue").ShouldFailWith("not your choice");
        game.Choose(0, "purple").ShouldFailWith("Choose one of");

        game.Choose(0, "Blue").ShouldSucceed();

        game.View().TopAttributes["color"].ShouldBe("blue");
        Rig.HandSize(game, 1).ShouldBe(5);
        game.View().CurrentSeat.ShouldBe(2);
        game.Play(2, Rig.Card(game, 2, "blue-1")).ShouldSucceed();
    }

    [Fact]
    public void An_unplayable_drawn_card_ends_the_turn()
    {
        var game = Rig.Classic();
        Rig.Hands(game, ["blue-7"], ["red-1"], ["red-2"]);
        Rig.Top(game, "red-3");
        Rig.NextDraws(game, "green-9");

        game.Pass(0).ShouldFailWith("only pass after drawing");
        game.Draw(0).ShouldSucceed();

        Rig.HandSize(game, 0).ShouldBe(2);
        game.View().CurrentSeat.ShouldBe(1);
    }

    [Fact]
    public void A_playable_drawn_card_may_be_played_or_kept_with_a_pass()
    {
        var game = Rig.Classic();
        Rig.Hands(game, ["red-5", "blue-7"], ["red-1"], ["red-2"]);
        Rig.Top(game, "red-3");
        Rig.NextDraws(game, "red-9", "red-8");

        game.Draw(0).ShouldSucceed();
        game.View().CurrentSeat.ShouldBe(0);
        game.Play(0, Rig.Card(game, 0, "red-5")).ShouldFailWith("only play the card you drew");
        game.Draw(0).ShouldFailWith("already drew");
        game.Pass(0).ShouldSucceed();
        game.View().CurrentSeat.ShouldBe(1);

        game.Draw(1).ShouldSucceed();
        game.Play(1, Rig.Card(game, 1, "red-8")).ShouldSucceed();
        game.View().CurrentSeat.ShouldBe(2);
    }

    [Fact]
    public void Draw_until_playable_keeps_drawing()
    {
        var game = Rig.Classic(3, s => s with { DrawStyle = DrawStyle.UntilPlayable });
        Rig.Hands(game, ["blue-7"], ["red-1"], ["red-2"]);
        Rig.Top(game, "red-3");
        Rig.NextDraws(game, "green-9", "yellow-9", "red-9");

        game.Draw(0).ShouldSucceed();

        Rig.HandSize(game, 0).ShouldBe(4);
        game.View().PlayableDrawnCardId.ShouldBe(Rig.Card(game, 0, "red-9"));
    }

    [Fact]
    public void A_player_caught_without_calling_uno_draws_the_penalty()
    {
        var game = Rig.Classic();
        Rig.Hands(game, ["red-5", "red-6"], ["red-1", "blue-1"], ["red-2", "blue-2"]);
        Rig.Top(game, "red-3");

        game.CallUno(1).ShouldFailWith("one card left");
        game.Play(0, Rig.Card(game, 0, "red-5")).ShouldSucceed();
        game.CatchUno(1, 0).ShouldSucceed();

        Rig.HandSize(game, 0).ShouldBe(3);
        game.Events.ShouldContain(new UnoCaught(1, 0, 2));
    }

    [Fact]
    public void Calling_uno_with_the_play_makes_the_player_safe()
    {
        var game = Rig.Classic();
        Rig.Hands(game, ["red-5", "red-6"], ["red-1", "blue-1"], ["red-2", "blue-2"]);
        Rig.Top(game, "red-3");

        game.Play(0, Rig.Card(game, 0, "red-5"), callUno: true).ShouldSucceed();

        game.CatchUno(1, 0).ShouldFailWith("is safe");
        game.Events.ShouldContain(new UnoCalled(0));
    }

    [Fact]
    public void First_player_out_wins_when_scoring_is_off()
    {
        var game = Rig.Classic();
        Rig.Hands(game, ["red-5"], ["red-1"], ["red-2"]);
        Rig.Top(game, "red-3");

        game.Play(0, Rig.Card(game, 0, "red-5")).ShouldSucceed();

        game.Phase.ShouldBe(GamePhase.GameOver);
        game.WinnerSeat.ShouldBe(0);
        game.Events.ShouldContain(new GameWon(0));
        game.Draw(1).ShouldFailWith("not in play");
    }

    [Fact]
    public void With_a_target_score_rounds_continue_until_someone_reaches_it()
    {
        var game = Rig.Classic(3, s => s with { Scoring = ScoringMode.TargetScore, TargetScore = 100 });
        Rig.Hands(game, ["red-5"], ["wild", "red-7"], ["red-skip"]);
        Rig.Top(game, "red-3");

        game.Play(0, Rig.Card(game, 0, "red-5")).ShouldSucceed();

        game.Phase.ShouldBe(GamePhase.RoundOver);
        game.Events.OfType<RoundWon>().Single().Points.ShouldBe(50 + 7 + 20);
        game.View().Players[0].Score.ShouldBe(77);
        game.Play(1, Rig.Card(game, 1, "red-7")).ShouldFailWith("not in play");

        game.StartNextRound().ShouldSucceed();
        var view = game.View();
        view.Round.ShouldBe(2);
        view.Players.ShouldAllBe(p => p.Hand.Count == 7);
        view.Players[0].Score.ShouldBe(77);
        game.ShouldConserveCards();
    }

    [Fact]
    public void Reaching_the_target_score_ends_the_game()
    {
        var game = Rig.Classic(3, s => s with { Scoring = ScoringMode.TargetScore, TargetScore = 60 });
        Rig.Hands(game, ["red-5"], ["wild"], ["red-skip"]);
        Rig.Top(game, "red-3");

        game.Play(0, Rig.Card(game, 0, "red-5")).ShouldSucceed();

        game.Phase.ShouldBe(GamePhase.GameOver);
        game.WinnerSeat.ShouldBe(0);
    }

    [Fact]
    public void A_first_card_with_abilities_acts_as_if_the_dealer_played_it()
    {
        var skips = new GameDefinitionBuilder("All skips")
            .Players(2, 4).HandSize(3).Attributes("color").MatchOnAny("color")
            .Card("red-skip", "Skip", c => c.With("color", "red").Copies(20).Ability("skip"))
            .Build();
        var game = new GameMediator(skips, ["P0", "P1", "P2"], random: new SeededRandom(1));

        game.Start().ShouldSucceed();

        game.Events.ShouldContain(new PlayerSkipped(0));
        game.View().CurrentSeat.ShouldBe(1);
    }

    [Fact]
    public void A_game_refuses_an_invalid_definition_or_player_count()
    {
        var tiny = new GameDefinitionBuilder("Tiny").Players(2, 2).HandSize(1).Attributes("color").MatchOnAny("color")
            .Card("red", "Red", c => c.With("color", "red").Copies(3)).Build();

        Should.Throw<GameRuleException>(() => new GameMediator(tiny, ["A", "B", "C"])).Message.ShouldContain("2 to 2 players");
        Should.Throw<DefinitionException>(() => new GameMediator(tiny with { MatchAttributes = [] }, ["A", "B"]));
    }
}
