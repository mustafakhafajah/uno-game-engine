namespace UnoEngine.Core.Definitions;

public enum ScoringMode
{
    /// <summary>The first player to empty their hand wins the game.</summary>
    FirstOutWins,

    /// <summary>Round winners collect the points left in the other hands; the first to the target score wins.</summary>
    TargetScore,
}

public enum DrawStyle
{
    OneCard,
    UntilPlayable,
}

public enum FirstCardRule
{
    /// <summary>The first card's abilities apply, as if the dealer had played it.</summary>
    ApplyAbilities,

    /// <summary>A first card with abilities (or a wild) goes back into the deck and another is flipped.</summary>
    DrawAnother,
}

/// <summary>
/// The choices the host makes for one game: scoring and house rules.
/// A game definition carries defaults; the host can override any of them when starting a game.
/// </summary>
public sealed record GameSettings
{
    public ScoringMode Scoring { get; init; } = ScoringMode.FirstOutWins;
    public int TargetScore { get; init; } = 500;

    /// <summary>A draw card (e.g. +2) may be answered with another draw card; the total passes on.</summary>
    public bool StackDrawCards { get; init; }

    /// <summary>A player who draws a playable card may play it straight away.</summary>
    public bool PlayDrawnCard { get; init; } = true;

    public DrawStyle DrawStyle { get; init; } = DrawStyle.OneCard;
    public FirstCardRule FirstCard { get; init; } = FirstCardRule.ApplyAbilities;

    /// <summary>Cards drawn by a player caught with one card who did not call UNO.</summary>
    public int UnoPenalty { get; init; } = 2;

    /// <summary>With two players, Reverse works like Skip (official Uno rule).</summary>
    public bool TwoPlayerReverseActsAsSkip { get; init; } = true;

    public static GameSettings Default { get; } = new();
}
