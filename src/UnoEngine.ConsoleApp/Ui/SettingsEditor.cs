using UnoEngine.Core.Definitions;

namespace UnoEngine.ConsoleApp.Ui;

/// <summary>Asks the host for scoring and house rules, starting from the given values.</summary>
internal static class SettingsEditor
{
    public static GameSettings Edit(GameSettings current)
    {
        Screen.Title("Scoring");
        var scoring = Screen.Menu("How is the game won?",
            ["First player out of cards wins", "Play rounds to a target score (winner of a round scores the cards left in other hands)"],
            current.Scoring == ScoringMode.TargetScore ? 1 : 0);
        var settings = current with { Scoring = scoring == 0 ? ScoringMode.FirstOutWins : ScoringMode.TargetScore };
        if (settings.Scoring == ScoringMode.TargetScore)
            settings = settings with { TargetScore = Screen.AskInt("Target score", 1, 100_000, current.TargetScore) };

        Screen.Title("House rules");
        settings = settings with
        {
            StackDrawCards = Screen.AskYesNo("Stack draw cards (+2 on +2, the total passes on)?", current.StackDrawCards),
            PlayDrawnCard = Screen.AskYesNo("May a playable drawn card be played straight away?", current.PlayDrawnCard),
            DrawStyle = Screen.Menu("When you can't play", ["Draw one card", "Draw until you get a playable card"],
                current.DrawStyle == DrawStyle.UntilPlayable ? 1 : 0) == 0 ? DrawStyle.OneCard : DrawStyle.UntilPlayable,
            FirstCard = Screen.Menu("If the first card has an ability", ["Apply it (as if the dealer played it)", "Flip another card"],
                current.FirstCard == FirstCardRule.DrawAnother ? 1 : 0) == 0 ? FirstCardRule.ApplyAbilities : FirstCardRule.DrawAnother,
            UnoPenalty = Screen.AskInt("Cards drawn when caught without calling UNO", 0, 20, current.UnoPenalty),
            TwoPlayerReverseActsAsSkip = Screen.AskYesNo("With two players, Reverse works like Skip?", current.TwoPlayerReverseActsAsSkip),
        };
        return settings;
    }

    public static string Summary(GameSettings s) =>
        string.Join(", ",
            s.Scoring == ScoringMode.FirstOutWins ? "first out wins" : $"play to {s.TargetScore} points",
            s.StackDrawCards ? "stacking on" : "no stacking",
            s.DrawStyle == DrawStyle.OneCard ? "draw one" : "draw until playable",
            s.PlayDrawnCard ? "drawn card playable" : "drawn card kept",
            $"UNO penalty {s.UnoPenalty}");
}
