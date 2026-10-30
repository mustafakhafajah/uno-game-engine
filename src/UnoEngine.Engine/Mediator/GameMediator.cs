using UnoEngine.Core.Cards;
using UnoEngine.Core.Definitions;
using UnoEngine.Core.Events;
using UnoEngine.Core.Randomness;
using UnoEngine.Engine.Commands;
using UnoEngine.Engine.Managers;
using UnoEngine.Engine.Views;
using UnoEngine.Rules.Definitions;
using UnoEngine.Rules.Effects;
using UnoEngine.Rules.Matching;

namespace UnoEngine.Engine.Mediator;

public enum GamePhase
{
    NotStarted,
    AwaitingPlay,
    AwaitingChoice,
    RoundOver,
    GameOver,
}

/// <summary>
/// The heart of the engine (Mediator pattern). Players send commands here; the mediator asks each
/// manager to do its part, in the right order, and publishes what happened as events.
/// The rules themselves live in the managers; the mediator only coordinates them.
/// </summary>
public sealed class GameMediator : IGameMediator
{
    private readonly List<GameEvent> _events = [];
    private readonly List<IGameCommand> _history = [];
    private EffectContext? _activeCard;
    private int _roundStartingSeat;

    public GameMediator(
        GameDefinition definition,
        IReadOnlyList<string> playerNames,
        GameSettings? settings = null,
        EffectRegistry? effects = null,
        IRandom? random = null)
    {
        effects ??= EffectRegistry.CreateDefault();
        GameDefinitionValidator.EnsureValid(definition, effects);
        if (playerNames.Count < definition.MinPlayers || playerNames.Count > definition.MaxPlayers)
            throw new GameRuleException($"{definition.Name} is for {definition.MinPlayers} to {definition.MaxPlayers} players, not {playerNames.Count}.");
        var needed = definition.HandSize * playerNames.Count + 1;
        if (definition.DeckSize < needed)
            throw new GameRuleException($"The deck has {definition.DeckSize} cards; {playerNames.Count} players need at least {needed}.");

        Definition = definition;
        Settings = settings ?? definition.DefaultSettings;
        Players = new PlayerManager(this);
        Deck = new DeckManager(this, random ?? new SeededRandom());
        Turns = new TurnManager(this);
        Rules = new RuleManager(this, new MatchAnyAttributeRule(definition.MatchAttributes), Settings);
        Effects = new EffectManager(this, effects);
        Choices = new ChoiceManager(this);
        Scores = new ScoreManager(this, Settings);
        Players.Seat(playerNames);
    }

    public GameDefinition Definition { get; }
    public GameSettings Settings { get; }
    public GamePhase Phase { get; private set; } = GamePhase.NotStarted;
    public int? WinnerSeat { get; private set; }

    /// <summary>Raised for every event, in order. Front-ends subscribe to redraw the table.</summary>
    public event Action<GameEvent>? EventRaised;

    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>Every successful command, in order: with the same seed, replaying them recreates the game.</summary>
    public IReadOnlyList<IGameCommand> History => _history;

    internal PlayerManager Players { get; }
    internal DeckManager Deck { get; }
    internal TurnManager Turns { get; }
    internal RuleManager Rules { get; }
    internal EffectManager Effects { get; }
    internal ChoiceManager Choices { get; }
    internal ScoreManager Scores { get; }

    void IGameMediator.Notify(GameEvent gameEvent) => Publish(gameEvent);

    /// <summary>Runs a player's command (Command pattern). On failure nothing changes and the reason is returned.</summary>
    public CommandResult Execute(IGameCommand command)
    {
        try
        {
            command.Execute(this);
            _history.Add(command);
            return CommandResult.Ok;
        }
        catch (GameRuleException ex)
        {
            return CommandResult.Fail(ex.Message);
        }
    }

    public CommandResult Start() => Execute(new StartGameCommand());
    public CommandResult Play(int seat, int cardId, bool callUno = false) => Execute(new PlayCardCommand(seat, cardId, callUno));
    public CommandResult Draw(int seat) => Execute(new DrawCardCommand(seat));
    public CommandResult Pass(int seat) => Execute(new PassCommand(seat));
    public CommandResult Choose(int seat, string option) => Execute(new ChooseCommand(seat, option));
    public CommandResult CallUno(int seat) => Execute(new CallUnoCommand(seat));
    public CommandResult CatchUno(int catcherSeat, int targetSeat) => Execute(new CatchUnoCommand(catcherSeat, targetSeat));
    public CommandResult StartNextRound() => Execute(new StartNextRoundCommand());

    /// <summary>A read-only snapshot of the table for front-ends and bots.</summary>
    public GameView View() => GameView.Of(this);

    /// <summary>Whether this card could be played right now by its holder (for highlighting playable cards).</summary>
    public bool CanPlay(int seat, int cardId) =>
        Phase == GamePhase.AwaitingPlay
        && seat == Turns.CurrentSeat
        && Players.Find(seat, cardId) is { } card
        && Rules.CanPlay(card, Deck.TopAttributes, Turns.HasDrawn, Turns.PlayableDrawnCard);

    // ---- command handlers: validate first, then change state -----------------------------------

    internal void HandleStart()
    {
        EnsurePhase(GamePhase.NotStarted, "The game has already started.");
        Players.ResetScores();
        _roundStartingSeat = 0;
        Deck.Reset(Deck.CreateCards(Definition));
        StartRound();
    }

    internal void HandleStartNextRound()
    {
        EnsurePhase(GamePhase.RoundOver, "The round is not over.");
        _roundStartingSeat = (_roundStartingSeat + 1) % Players.Count;
        Deck.Reset(Players.CollectAllCards().Concat(Deck.CollectAllCards()));
        StartRound();
    }

    internal void HandlePlay(int seat, int cardId, bool callUno)
    {
        EnsureTurn(seat);
        var card = Players.Find(seat, cardId) ?? throw new GameRuleException("You don't have that card.");
        if (Rules.WhyNotPlayable(card, Deck.TopAttributes, Turns.HasDrawn, Turns.PlayableDrawnCard) is { } reason)
            throw new GameRuleException(reason);

        Players.Take(seat, cardId);
        Deck.Discard(card);
        Publish(new CardPlayed(seat, card));
        if (callUno && Players.CanCallUno(seat, isTheirTurn: true))
            CallUnoFor(seat);
        RunCard(card);
    }

    internal void HandleDraw(int seat)
    {
        EnsureTurn(seat);
        if (Turns.HasDrawn)
            throw new GameRuleException("You already drew this turn; play the card you drew or pass.");

        if (Rules.PendingDraw > 0)
        {
            GiveCards(seat, Rules.TakePendingDraw(), DrawReason.StackedPenalty);
            AdvanceTurn();
            return;
        }

        var drawn = new List<Card>();
        do
        {
            var card = Deck.Draw(1).SingleOrDefault();
            if (card is null)
                break;
            drawn.Add(card);
        }
        while (Settings.DrawStyle == DrawStyle.UntilPlayable && !Rules.Matches(drawn[^1], Deck.TopAttributes));

        Players.Give(seat, drawn);
        Publish(new CardsDrawn(seat, drawn.Count, DrawReason.Draw));

        var last = drawn.LastOrDefault();
        if (last is not null && Settings.PlayDrawnCard && Rules.Matches(last, Deck.TopAttributes))
            Turns.MarkDrawn(last);
        else
            AdvanceTurn();
    }

    internal void HandlePass(int seat)
    {
        EnsureTurn(seat);
        if (!Turns.HasDrawn)
            throw new GameRuleException("You can only pass after drawing a card.");
        Publish(new TurnPassed(seat));
        AdvanceTurn();
    }

    internal void HandleChoose(int seat, string option)
    {
        EnsurePhase(GamePhase.AwaitingChoice, "No choice is waiting.");
        var (answer, resume) = Choices.Resolve(seat, option);
        var waiting = Effects.Continue(_activeCard!, resume, answer);
        AfterEffects(waiting);
    }

    internal void HandleCallUno(int seat)
    {
        EnsureSeat(seat);
        EnsureInRound();
        var isTheirTurn = Phase == GamePhase.AwaitingPlay && seat == Turns.CurrentSeat;
        if (!Players.CanCallUno(seat, isTheirTurn))
            throw new GameRuleException(Players[seat].CalledUno
                ? "You already called UNO."
                : "You can call UNO with one card left, or with two just before you play.");
        CallUnoFor(seat);
    }

    internal void HandleCatchUno(int catcherSeat, int targetSeat)
    {
        EnsureSeat(catcherSeat);
        EnsureSeat(targetSeat);
        EnsureInRound();
        if (catcherSeat == targetSeat)
            throw new GameRuleException("You can't catch yourself.");
        if (!Players.IsUnoExposed(targetSeat))
            throw new GameRuleException($"{Players[targetSeat].Name} is safe.");
        var penalty = Settings.UnoPenalty;
        Publish(new UnoCaught(catcherSeat, targetSeat, penalty));
        GiveCards(targetSeat, penalty, DrawReason.UnoPenalty);
    }

    // ---- the flow between managers -------------------------------------------------------------

    private void StartRound()
    {
        Scores.StartRound();
        Rules.ClearPendingDraw();
        Effects.Clear();
        Choices.Clear();
        Turns.Reset(Players.Count, _roundStartingSeat);
        Publish(new RoundStarted(Scores.Round, _roundStartingSeat));

        foreach (var _ in Enumerable.Range(0, Definition.HandSize))
        {
            foreach (var seat in Turns.OrderFrom(_roundStartingSeat))
                Players.Give(seat, Deck.Draw(1));
        }

        foreach (var seat in Turns.OrderFrom(_roundStartingSeat))
            Publish(new CardsDrawn(seat, Definition.HandSize, DrawReason.Deal));

        var first = Deck.FlipFirstCard();
        if (Settings.FirstCard == FirstCardRule.DrawAnother)
        {
            for (var tries = 0; HasAbilities(first) && tries < Definition.DeckSize; tries++)
            {
                Deck.ReturnTopCardAndShuffle();
                first = Deck.FlipFirstCard();
            }
        }

        Publish(new FirstCardFlipped(first));
        Phase = GamePhase.AwaitingPlay;

        if (Settings.FirstCard == FirstCardRule.ApplyAbilities && HasAbilities(first))
        {
            // The first card acts as if the dealer (the seat before the starting player) had played it.
            Turns.SetCurrent(Turns.SeatAfter(_roundStartingSeat, -1));
            RunCard(first);
        }
        else
        {
            Publish(new TurnChanged(_roundStartingSeat));
        }
    }

    private static bool HasAbilities(Card card) => card.IsWild || card.Definition.Abilities.Count > 0;

    private void RunCard(Card card)
    {
        _activeCard = new EffectContext(this, card);
        Effects.Load(card);
        AfterEffects(Effects.RunUntilChoice(_activeCard));
    }

    private void AfterEffects(EffectOutcome.AwaitingChoice? waiting)
    {
        if (waiting is not null)
        {
            Phase = GamePhase.AwaitingChoice;
            Choices.Await(waiting);
            return;
        }

        _activeCard = null;
        Phase = GamePhase.AwaitingPlay;
        var seat = Turns.CurrentSeat;
        if (Players[seat].Hand.Count == 0)
            EndRound(seat);
        else
            AdvanceTurn();
    }

    private void AdvanceTurn() => Publish(new TurnChanged(Turns.EndTurn()));

    private void EndRound(int winnerSeat)
    {
        var others = Players.Players.Where(p => p.Seat != winnerSeat).Select(p => p.Hand).ToList();
        var result = Scores.FinishRound(winnerSeat, Players[winnerSeat].Score, others);
        Players.AddScore(winnerSeat, result.Points);
        Rules.ClearPendingDraw();
        Publish(new RoundWon(winnerSeat, result.Points, Players.Players.ToDictionary(p => p.Seat, p => p.Score)));

        if (result.GameOver)
        {
            Phase = GamePhase.GameOver;
            WinnerSeat = winnerSeat;
            Publish(new GameWon(winnerSeat));
        }
        else
        {
            Phase = GamePhase.RoundOver;
        }
    }

    internal void GiveCards(int seat, int count, DrawReason reason)
    {
        var cards = Deck.Draw(count);
        Players.Give(seat, cards);
        Publish(new CardsDrawn(seat, cards.Count, reason));
    }

    private void CallUnoFor(int seat)
    {
        Players.CallUno(seat);
        Publish(new UnoCalled(seat));
    }

    private void Publish(GameEvent gameEvent)
    {
        _events.Add(gameEvent);
        EventRaised?.Invoke(gameEvent);
    }

    private void EnsurePhase(GamePhase phase, string message)
    {
        if (Phase != phase)
            throw new GameRuleException(message);
    }

    private void EnsureTurn(int seat)
    {
        EnsureSeat(seat);
        if (Phase == GamePhase.AwaitingChoice)
            throw new GameRuleException($"Waiting for {Players[Choices.Pending!.Seat].Name} to choose.");
        EnsurePhase(GamePhase.AwaitingPlay, "The game is not in play.");
        if (seat != Turns.CurrentSeat)
            throw new GameRuleException($"It is {Players[Turns.CurrentSeat].Name}'s turn.");
    }

    private void EnsureSeat(int seat)
    {
        if (seat < 0 || seat >= Players.Count)
            throw new GameRuleException($"There is no player in seat {seat}.");
    }

    private void EnsureInRound()
    {
        if (Phase is not (GamePhase.AwaitingPlay or GamePhase.AwaitingChoice))
            throw new GameRuleException("The round is not in play.");
    }
}
