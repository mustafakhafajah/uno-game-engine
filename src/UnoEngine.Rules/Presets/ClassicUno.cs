using UnoEngine.Core.Definitions;
using UnoEngine.Rules.Definitions;

namespace UnoEngine.Rules.Presets;

/// <summary>The official 108-card Uno deck, built only from the engine's general building blocks.</summary>
public static class ClassicUno
{
    public static readonly string[] Colors = ["red", "yellow", "green", "blue"];

    public static GameDefinition Create()
    {
        var builder = new GameDefinitionBuilder("Classic Uno")
            .Description("The official 108-card Uno deck.")
            .Players(2, 10)
            .HandSize(7)
            .Attributes("color", "symbol")
            .MatchOnAny("color", "symbol");

        foreach (var color in Colors)
        {
            builder.Card($"{color}-0", "0", c => c.With("color", color).With("symbol", "0").Image($"cards/{color}-0.png"));
            for (var n = 1; n <= 9; n++)
            {
                var number = n;
                builder.Card($"{color}-{number}", $"{number}", c => c.With("color", color).With("symbol", $"{number}")
                    .Copies(2).Points(number).Image($"cards/{color}-{number}.png"));
            }

            builder.Card($"{color}-skip", "Skip", c => c.With("color", color).With("symbol", "skip")
                .Copies(2).Points(20).Image($"cards/{color}-skip.png").Ability("skip", ("count", 1)));
            builder.Card($"{color}-reverse", "Reverse", c => c.With("color", color).With("symbol", "reverse")
                .Copies(2).Points(20).Image($"cards/{color}-reverse.png").Ability("reverse"));
            builder.Card($"{color}-draw2", "+2", c => c.With("color", color).With("symbol", "+2")
                .Copies(2).Points(20).Image($"cards/{color}-draw2.png")
                .Ability("draw", ("target", "next"), ("count", 2), ("losesTurn", true)));
        }

        builder.Card("wild", "Wild", c => c.With("symbol", "wild").Wild()
            .Copies(4).Points(50).Image("cards/wild.png").Ability("chooseAttribute", ("attribute", "color")));
        builder.Card("wild-draw4", "Wild +4", c => c.With("symbol", "wild-draw4").Wild()
            .Copies(4).Points(50).Image("cards/wild-draw4.png")
            .Ability("chooseAttribute", ("attribute", "color"))
            .Ability("draw", ("target", "next"), ("count", 4), ("losesTurn", true)));

        return builder.Build();
    }
}
