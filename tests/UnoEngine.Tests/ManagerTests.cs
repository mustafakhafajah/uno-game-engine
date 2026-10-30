using UnoEngine.Core.Cards;
using UnoEngine.Core.Definitions;
using UnoEngine.Core.Events;
using UnoEngine.Core.Randomness;
using UnoEngine.Engine.Managers;
using UnoEngine.Engine.Mediator;
using UnoEngine.Rules.Matching;
using UnoEngine.Rules.Presets;

namespace UnoEngine.Tests;

/// <summary>Each manager on its own, with a mediator stub that records what it is told.</summary>
public class ManagerTests
{
    private sealed class RecordingMediator : IGameMediator
    {
        public List<GameEvent> Events { get; } = [];

        public void Notify(GameEvent gameEvent) => Events.Add(gameEvent);
    }

    private readonly RecordingMediator _mediator = new();

    private static Card Make(int id, string color, string symbol, params AbilityDefinition[] abilities) =>
        new(id, new CardDefinition($"{color}-{symbol}", symbol,
            new Dictionary<string, string> { ["color"] = color, ["symbol"] = symbol }, abilities));

    [Fact]
    public void Turn_manager_moves_skips_and_reverses()
    {
        var turns = new TurnManager(_mediator);
        turns.Reset(playerCount: 4, startingSeat: 0);

        turns.EndTurn().ShouldBe(1);
        turns.Skip(1);
        turns.EndTurn().ShouldBe(3);
        _mediator.Events.ShouldContain(new PlayerSkipped(2));

        turns.Reverse();
        turns.OrderFrom(3).ShouldBe([3, 2, 1, 0]);
        turns.EndTurn().ShouldBe(2);

        turns.GrantPlayAgain();
        turns.EndTurn().ShouldBe(2);
    }

    [Fact]
    public void Deck_manager_reshuffles_the_discard_pile_but_keeps_the_top_card()
    {
        var deck = new DeckManager(_mediator, new SeededRandom(1));
        deck.Reset(Enumerable.Range(1, 5).Select(i => Make(i, "red", $"{i}")));

        var top = deck.FlipFirstCard();
        foreach (var card in deck.Draw(4))
            deck.DiscardUnderTop([card]);
        deck.DrawPileCount.ShouldBe(0);

        var drawn = deck.Draw(2);

        drawn.Count.ShouldBe(2);
        deck.TopCard.ShouldBeSameAs(top);
        _mediator.Events.OfType<DeckReshuffled>().Single().CardCount.ShouldBe(4);
    }

    [Fact]
    public void Deck_manager_layers_a_chosen_colour_over_the_top_card_until_the_next_discard()
    {
        var deck = new DeckManager(_mediator, new SeededRandom(1));
        deck.Reset([]);
        deck.Discard(Make(1, "none", "wild"));
        deck.ChooseAttribute("color", "blue");

        deck.TopAttributes["color"].ShouldBe("blue");
        deck.Discard(Make(2, "red", "5"));
        deck.TopAttributes["color"].ShouldBe("red");
    }

    [Fact]
    public void Player_manager_swaps_and_rotates_hands()
    {
        var players = new PlayerManager(_mediator);
        players.Seat(["A", "B", "C"]);
        players.Give(0, [Make(1, "red", "1")]);
        players.Give(1, [Make(2, "red", "2"), Make(3, "red", "3")]);

        players.SwapHands(0, 1);
        players[0].Hand.Count.ShouldBe(2);
        players[1].Hand.Single().InstanceId.ShouldBe(1);

        players.RotateHands([0, 1, 2]);
        players[1].Hand.Count.ShouldBe(2);
        players[2].Hand.Single().InstanceId.ShouldBe(1);
        players[0].Hand.ShouldBeEmpty();
    }

    [Fact]
    public void Player_manager_tracks_uno_and_clears_it_when_cards_arrive()
    {
        var players = new PlayerManager(_mediator);
        players.Seat(["A", "B"]);
        players.Give(0, [Make(1, "red", "1")]);

        players.IsUnoExposed(0).ShouldBeTrue();
        players.CallUno(0);
        players.IsUnoExposed(0).ShouldBeFalse();

        players.Give(0, [Make(2, "red", "2")]);
        players[0].CalledUno.ShouldBeFalse();
    }

    [Fact]
    public void Rule_manager_matches_colour_or_symbol_and_always_allows_wilds()
    {
        var rules = new RuleManager(_mediator, new MatchAnyAttributeRule(["color", "symbol"]), GameSettings.Default);
        var top = new Dictionary<string, string> { ["color"] = "red", ["symbol"] = "5" };
        var wild = new Card(9, ClassicUno.Create().Cards.Single(c => c.Id == "wild"));

        rules.CanPlay(Make(1, "red", "9"), top, false, null).ShouldBeTrue();
        rules.CanPlay(Make(2, "blue", "5"), top, false, null).ShouldBeTrue();
        rules.CanPlay(wild, top, false, null).ShouldBeTrue();
        rules.WhyNotPlayable(Make(3, "blue", "7"), top, false, null).ShouldNotBeNull().ShouldContain("does not match");
    }

    [Fact]
    public void Rule_manager_only_accepts_draw_cards_while_a_penalty_is_stacked()
    {
        var rules = new RuleManager(_mediator, new MatchAnyAttributeRule(["color"]), GameSettings.Default with { StackDrawCards = true });
        var top = new Dictionary<string, string> { ["color"] = "red" };
        var plusTwo = Make(1, "blue", "draw2", AbilityDefinition.Of("draw", ("target", "next"), ("count", 2), ("losesTurn", true)));

        rules.AddPendingDraw(2);

        rules.CanPlay(Make(2, "red", "3"), top, false, null).ShouldBeFalse();
        rules.CanPlay(plusTwo, top, false, null).ShouldBeTrue();
        rules.TakePendingDraw().ShouldBe(2);
        rules.PendingDraw.ShouldBe(0);
    }

    [Fact]
    public void Score_manager_ends_the_game_on_first_out_or_at_the_target()
    {
        var hand = new List<Card> { new(1, ClassicUno.Create().Cards.Single(c => c.Id == "wild")) };

        new ScoreManager(_mediator, GameSettings.Default).FinishRound(0, 0, [hand]).GameOver.ShouldBeTrue();

        var scoring = new ScoreManager(_mediator, GameSettings.Default with { Scoring = ScoringMode.TargetScore, TargetScore = 100 });
        scoring.FinishRound(0, 0, [hand]).ShouldBe(new RoundResult(0, 50, false));
        scoring.FinishRound(0, 50, [hand]).GameOver.ShouldBeTrue();
    }
}
