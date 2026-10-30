using UnoEngine.Core.Definitions;
using UnoEngine.Rules.Definitions;
using UnoEngine.Rules.Effects;
using UnoEngine.Rules.Presets;
using UnoEngine.Tests.Support;

namespace UnoEngine.Tests;

public class DefinitionTests
{
    private readonly EffectRegistry _effects = EffectRegistry.CreateDefault();

    [Fact]
    public void Classic_uno_has_the_official_108_cards()
    {
        var classic = ClassicUno.Create();

        classic.DeckSize.ShouldBe(108);
        classic.Cards.Where(c => c.Wild).Sum(c => c.Copies).ShouldBe(8);
        classic.ValuesOf("color").ShouldBe(["red", "yellow", "green", "blue"], ignoreOrder: true);
        GameDefinitionValidator.Validate(classic, _effects).ShouldBeEmpty();
    }

    [Fact]
    public void The_classic_json_preset_matches_the_builder()
    {
        var fromJson = Rig.LoadPreset("classic-uno.json");
        var fromCode = ClassicUno.Create();

        fromJson.DeckSize.ShouldBe(108);
        fromJson.Cards.Select(c => (c.Id, c.Copies, c.Points, c.Abilities.Count))
            .ShouldBe(fromCode.Cards.Select(c => (c.Id, c.Copies, c.Points, c.Abilities.Count)));
        GameDefinitionValidator.Validate(fromJson, _effects).ShouldBeEmpty();
    }

    [Fact]
    public void The_animal_party_preset_is_valid()
    {
        var party = Rig.LoadPreset("animal-party.json");

        GameDefinitionValidator.Validate(party, _effects).ShouldBeEmpty();
        party.DefaultSettings.Scoring.ShouldBe(ScoringMode.TargetScore);
        party.DefaultSettings.TargetScore.ShouldBe(100);
        party.Cards.Single(c => c.Id == "sly-fox").Abilities.Single().Type.ShouldBe("swapHands");
    }

    [Fact]
    public void Json_round_trip_keeps_cards_abilities_and_settings()
    {
        var original = Rig.LoadPreset("animal-party.json");

        var copy = GameDefinitionLoader.FromJson(GameDefinitionLoader.ToJson(original));

        copy.Name.ShouldBe(original.Name);
        copy.DefaultSettings.ShouldBe(original.DefaultSettings);
        copy.Cards.Select(c => c.Image).ShouldBe(original.Cards.Select(c => c.Image));
        copy.Cards.SelectMany(c => c.Abilities).Select(a => a.ToString())
            .ShouldBe(original.Cards.SelectMany(c => c.Abilities).Select(a => a.ToString()));
    }

    [Fact]
    public void The_validator_names_every_problem_with_its_card()
    {
        var broken = new GameDefinitionBuilder("Broken")
            .Players(2, 4).HandSize(3)
            .Attributes("color").MatchOnAny("color", "shape")
            .Card("a", "A", c => c.With("color", "red").With("size", "big").Copies(2))
            .Card("a", "A again", c => c.With("color", "red"))
            .Card("b", "B", c => c.With("color", "red").Ability("teleport"))
            .Card("c", "C", c => c.With("color", "red").Ability("draw", ("count", "lots")))
            .Card("d", "D", c => c.Ability("chooseAttribute", ("attribute", "mood")))
            .Build();

        var errors = GameDefinitionValidator.Validate(broken, _effects);

        errors.ShouldContain(e => e.Contains("'shape', which is not a declared attribute"));
        errors.ShouldContain(e => e.Contains("card 'a': 'size' is not a declared attribute"));
        errors.ShouldContain(e => e.Contains("'a' is used 2 times"));
        errors.ShouldContain(e => e.Contains("card 'b': unknown ability 'teleport'"));
        errors.ShouldContain(e => e.Contains("card 'c'") && e.Contains("whole number"));
        errors.ShouldContain(e => e.Contains("card 'd'") && e.Contains("could never be played"));
        errors.ShouldContain(e => e.Contains("card 'd'") && e.Contains("'mood'"));
        errors.ShouldContain(e => e.Contains("the deck has 6 cards"));
    }

    [Fact]
    public void Broken_json_gives_a_readable_error()
    {
        Should.Throw<DefinitionException>(() => GameDefinitionLoader.FromJson("{ \"name\": "))
            .Message.ShouldContain("could not be read");
        Should.Throw<DefinitionException>(() => GameDefinitionLoader.FromJson("""{ "cards": [ { "abilities": [ { "count": 2 } ] } ], "name": "x" }"""))
            .Message.ShouldContain("needs a 'type'");
    }
}
