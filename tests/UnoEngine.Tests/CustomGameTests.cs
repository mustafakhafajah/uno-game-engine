using UnoEngine.Core.Choices;
using UnoEngine.Core.Events;
using UnoEngine.Engine.Mediator;
using UnoEngine.Rules.Definitions;
using UnoEngine.Rules.Effects;
using UnoEngine.Tests.Support;

namespace UnoEngine.Tests;

/// <summary>A game nobody hard-coded: the Animal Party preset uses the engine's other building blocks.</summary>
public class CustomGameTests
{
    private static GameMediator Party(int players = 3) => Rig.Start(Rig.LoadPreset("animal-party.json"), players);

    [Fact]
    public void Sly_fox_swaps_hands_with_a_chosen_player()
    {
        var game = Party();
        Rig.Hands(game, ["sly-fox", "cat-city"], ["dog-city"], ["owl-forest", "owl-city", "frog-river"]);
        Rig.Top(game, "owl-forest");

        game.Play(0, Rig.Card(game, 0, "sly-fox")).ShouldSucceed();
        var choice = game.View().PendingChoice.ShouldNotBeNull();
        choice.Kind.ShouldBe(ChoiceKind.Player);
        choice.Options.ShouldBe(["1", "2"]);

        game.Choose(0, "2").ShouldSucceed();

        Rig.HandSize(game, 0).ShouldBe(3);
        game.View().Players[2].Hand.Single().Definition.Id.ShouldBe("cat-city");
        game.View().CurrentSeat.ShouldBe(1);
        game.ShouldConserveCards();
    }

    [Fact]
    public void Eagle_gives_another_turn()
    {
        var game = Party();
        Rig.Hands(game, ["eagle", "dog-city", "owl-forest"], ["cat-city"], ["frog-river"]);
        Rig.Top(game, "cat-city");

        game.Play(0, Rig.Card(game, 0, "eagle")).ShouldSucceed();
        game.View().CurrentSeat.ShouldBe(0);
        game.Play(0, Rig.Card(game, 0, "dog-city")).ShouldSucceed();
        game.View().CurrentSeat.ShouldBe(1);
    }

    [Fact]
    public void Bear_skips_two_players()
    {
        var game = Party(4);
        Rig.Hands(game, ["bear", "owl-forest"], ["cat-city"], ["dog-city"], ["frog-river"]);
        Rig.Top(game, "owl-forest");

        game.Play(0, Rig.Card(game, 0, "bear")).ShouldSucceed();

        game.View().CurrentSeat.ShouldBe(3);
        game.Events.OfType<PlayerSkipped>().Select(e => e.Seat).ShouldBe([1, 2]);
    }

    [Fact]
    public void Flood_makes_everyone_else_draw()
    {
        var game = Party();
        Rig.Hands(game, ["flood", "cat-city"], ["dog-city"], ["owl-city"]);
        Rig.Top(game, "frog-river");

        game.Play(0, Rig.Card(game, 0, "flood")).ShouldSucceed();

        Rig.HandSize(game, 0).ShouldBe(1);
        Rig.HandSize(game, 1).ShouldBe(2);
        Rig.HandSize(game, 2).ShouldBe(2);
    }

    [Fact]
    public void Cat_feast_discards_every_other_cat_too()
    {
        var game = Party();
        Rig.Hands(game, ["cat-feast", "cat-city", "cat-forest", "dog-city"], ["owl-city"], ["frog-river"]);
        Rig.Top(game, "dog-city");

        game.Play(0, Rig.Card(game, 0, "cat-feast")).ShouldSucceed();

        game.View().Players[0].Hand.Single().Definition.Id.ShouldBe("dog-city");
        game.View().TopCard!.Definition.Id.ShouldBe("cat-feast");
        game.Events.ShouldContain(new CardsDiscarded(0, 2));
        game.ShouldConserveCards();
    }

    [Fact]
    public void Migration_chooses_the_habitat_to_match()
    {
        var game = Party();
        Rig.Hands(game, ["migration", "cat-city"], ["cat-forest", "frog-river"], ["owl-city"]);
        Rig.Top(game, "dog-city");

        game.Play(0, Rig.Card(game, 0, "migration")).ShouldSucceed();
        game.View().PendingChoice!.Options.ShouldBe(["city", "forest", "river"], ignoreOrder: true);
        game.Choose(0, "river").ShouldSucceed();

        game.Play(1, Rig.Card(game, 1, "cat-forest")).ShouldFailWith("does not match");
        game.Play(1, Rig.Card(game, 1, "frog-river")).ShouldSucceed();
    }

    [Fact]
    public void Stampede_passes_every_hand_along_and_accepts_any_card_after_it()
    {
        var game = Party();
        Rig.Hands(game, ["stampede", "cat-city"], ["dog-river"], ["owl-forest", "owl-city"]);
        Rig.Top(game, "frog-river");

        game.Play(0, Rig.Card(game, 0, "stampede")).ShouldSucceed();

        game.View().Players[0].Hand.Select(c => c.Definition.Id).ShouldBe(["owl-forest", "owl-city"], ignoreOrder: true);
        game.View().Players[1].Hand.Single().Definition.Id.ShouldBe("cat-city");
        game.View().Players[2].Hand.Single().Definition.Id.ShouldBe("dog-river");
        game.Play(1, Rig.Card(game, 1, "cat-city")).ShouldSucceed();
    }

    /// <summary>A custom building block: the next player draws one card for every card you hold.</summary>
    private sealed class DrawForEachEffect : ICardEffect
    {
        public string Type => "drawForEach";

        public EffectOutcome Apply(IEffectContext context)
        {
            context.DrawCards(context.NextSeat(), context.HandSize(context.CurrentSeat));
            return EffectOutcome.Done;
        }
    }

    [Fact]
    public void A_plugin_effect_can_be_registered_and_used_without_changing_the_engine()
    {
        var definition = new GameDefinitionBuilder("Plugin demo")
            .Players(2, 4).HandSize(3).Attributes("color").MatchOnAny("color")
            .Card("red", "Red", c => c.With("color", "red").Copies(20))
            .Card("red-hungry", "Hungry", c => c.With("color", "red").Copies(2).Ability("drawForEach"))
            .Build();

        Should.Throw<DefinitionException>(() => Rig.Start(definition, 2)).Message.ShouldContain("unknown ability 'drawForEach'");

        var effects = EffectRegistry.CreateDefault().Register("drawForEach", _ => new DrawForEachEffect());
        var game = Rig.Start(definition, 2, effects: effects);
        Rig.Hands(game, ["red-hungry", "red", "red", "red"], ["red"]);
        Rig.Top(game, "red");

        game.Play(0, Rig.Card(game, 0, "red-hungry")).ShouldSucceed();

        Rig.HandSize(game, 1).ShouldBe(4);
    }
}
