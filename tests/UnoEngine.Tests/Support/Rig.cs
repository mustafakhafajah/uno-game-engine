using UnoEngine.Core.Definitions;
using UnoEngine.Core.Randomness;
using UnoEngine.Engine.Mediator;
using UnoEngine.Rules.Definitions;
using UnoEngine.Rules.Effects;
using UnoEngine.Rules.Presets;

namespace UnoEngine.Tests.Support;

/// <summary>Sets up exact game situations (hands, top card, next draws) for scripted tests.</summary>
internal static class Rig
{
    public static readonly string PresetsFolder = FindPresets();

    /// <summary>A started game with a plain first card, so seat 0 is to play.</summary>
    public static GameMediator Start(GameDefinition definition, int players, Func<GameSettings, GameSettings>? settings = null, int seed = 7, EffectRegistry? effects = null)
    {
        var configured = (settings ?? (s => s))(definition.DefaultSettings with { FirstCard = FirstCardRule.DrawAnother });
        var names = Enumerable.Range(0, players).Select(i => $"P{i}").ToList();
        var game = new GameMediator(definition, names, configured, effects, new SeededRandom(seed));
        game.Start().Succeeded.ShouldBeTrue();
        return game;
    }

    public static GameMediator Classic(int players = 3, Func<GameSettings, GameSettings>? settings = null) =>
        Start(ClassicUno.Create(), players, settings);

    /// <summary>Replaces every hand: hands[seat] lists card definition ids.</summary>
    public static void Hands(GameMediator game, params string[][] hands)
    {
        foreach (var player in game.Players.Players)
            game.Deck.ReturnToDrawPile(player.TakeAll());
        for (var seat = 0; seat < hands.Length; seat++)
        {
            foreach (var id in hands[seat])
                game.Players.Give(seat, [game.Deck.TakeFromDrawPile(id)]);
        }
    }

    /// <summary>Puts a specific card on top of the discard pile.</summary>
    public static void Top(GameMediator game, string id) => game.Deck.Discard(game.Deck.TakeFromDrawPile(id));

    /// <summary>The next cards drawn will be these, in this order.</summary>
    public static void NextDraws(GameMediator game, params string[] ids)
    {
        foreach (var id in ids.Reverse())
            game.Deck.PutOnTopOfDrawPile(game.Deck.TakeFromDrawPile(id));
    }

    /// <summary>The instance id of the first card with this definition id in a seat's hand.</summary>
    public static int Card(GameMediator game, int seat, string id) =>
        game.Players[seat].Hand.First(c => c.Definition.Id == id).InstanceId;

    public static int HandSize(GameMediator game, int seat) => game.Players[seat].Hand.Count;

    /// <summary>No card is ever created or lost: hands + draw pile + discard pile = deck size.</summary>
    public static void ShouldConserveCards(this GameMediator game) =>
        (game.Players.Players.Sum(p => p.Hand.Count) + game.Deck.DrawPileCount + game.Deck.DiscardPileCount)
            .ShouldBe(game.Definition.DeckSize);

    public static void ShouldSucceed(this Engine.Commands.CommandResult result) =>
        result.Succeeded.ShouldBeTrue(result.Error);

    public static void ShouldFailWith(this Engine.Commands.CommandResult result, string fragment)
    {
        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain(fragment, Case.Insensitive);
    }

    public static GameDefinition LoadPreset(string file) => GameDefinitionLoader.Load(Path.Combine(PresetsFolder, file));

    private static string FindPresets()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "presets");
            if (File.Exists(Path.Combine(candidate, "classic-uno.json")))
                return candidate;
        }

        throw new DirectoryNotFoundException("presets folder not found");
    }
}
