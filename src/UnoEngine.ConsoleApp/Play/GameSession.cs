using UnoEngine.ConsoleApp.Ui;
using UnoEngine.Core.Choices;
using UnoEngine.Engine.Bots;
using UnoEngine.Engine.Commands;
using UnoEngine.Engine.Mediator;
using UnoEngine.Engine.Views;
using UnoEngine.Rules.Definitions;

namespace UnoEngine.ConsoleApp.Play;

/// <summary>Sets up a game with the host's choices and runs it: people take turns at the keyboard, bots play themselves.</summary>
internal sealed class GameSession
{
    private readonly GameMediator _game;
    private readonly HashSet<int> _bots;
    private readonly SimpleBot _bot = new();
    private int? _lastHumanShown;

    private GameSession(GameMediator game, HashSet<int> bots)
    {
        _game = game;
        _bots = bots;
    }

    public static void SetUpAndRun()
    {
        Screen.Title("New game");
        if (GameLibrary.Pick("Which game?") is not { } picked)
            return;
        var definition = picked.Definition;
        if (definition.Description is { } description)
            Screen.Dim(description);

        var count = Screen.AskInt("How many players", definition.MinPlayers, definition.MaxPlayers, Math.Min(Math.Max(3, definition.MinPlayers), definition.MaxPlayers));
        var names = new List<string>();
        var bots = new HashSet<int>();
        for (var seat = 0; seat < count; seat++)
        {
            var isBot = seat > 0 && Screen.AskYesNo($"Is player {seat + 1} a computer?", true);
            names.Add(isBot ? $"Bot {seat}" : Screen.Ask($"Name of player {seat + 1}", $"Player {seat + 1}"));
            if (isBot)
                bots.Add(seat);
        }

        var settings = definition.DefaultSettings;
        Screen.Info($"Rules: {SettingsEditor.Summary(settings)}");
        if (!Screen.AskYesNo("Play with these rules?", true))
            settings = SettingsEditor.Edit(settings);

        GameMediator game;
        try
        {
            game = new GameMediator(definition, names, settings);
        }
        catch (Exception ex) when (ex is DefinitionException or GameRuleException)
        {
            Screen.Error(ex.Message);
            return;
        }

        new GameSession(game, bots).Run();
    }

    private void Run()
    {
        var printer = new EventFormatter(_game);
        _game.EventRaised += printer.Print;
        _game.Start();

        while (_game.Phase != GamePhase.GameOver)
        {
            if (_game.Phase == GamePhase.RoundOver)
            {
                Screen.Pause("Press Enter for the next round");
                _game.StartNextRound();
                continue;
            }

            var view = _game.View();
            var seat = view.PendingChoice?.Seat ?? view.CurrentSeat;
            BotsCatchForgottenUno(view, seat);

            if (_bots.Contains(seat))
            {
                var command = _bot.Decide(_game, seat);
                if (command is not null)
                    _game.Execute(command);
                continue;
            }

            if (!HumanTurn(seat))
                return;
        }

        Screen.Pause();
    }

    /// <summary>Bots catch a player who has one card and forgot UNO, once that player's turn is over.</summary>
    private void BotsCatchForgottenUno(GameView view, int actingSeat)
    {
        foreach (var player in view.Players.Where(p => p.Seat != actingSeat && p.Hand.Count == 1 && !p.CalledUno))
        {
            var catcher = _bots.FirstOrDefault(b => b != player.Seat, -1);
            if (catcher >= 0)
                _game.CatchUno(catcher, player.Seat);
        }
    }

    /// <summary>One action by a person. Returns false when they quit.</summary>
    private bool HumanTurn(int seat)
    {
        var view = _game.View();
        var humans = view.Players.Count - _bots.Count;
        if (humans > 1 && _lastHumanShown != seat)
        {
            Screen.Pause($"\n{view.Players[seat].Name}, your turn (others look away). Press Enter");
            Screen.Clear();
        }

        _lastHumanShown = seat;
        ShowTable(view, seat);

        if (view.PendingChoice is { } choice)
        {
            var labels = choice.Options.Select(o => choice.Kind == ChoiceKind.Player ? view.Players[int.Parse(o)].Name : o).ToList();
            var picked = Screen.Menu(choice.Prompt, labels);
            Report(_game.Choose(seat, choice.Options[picked]));
            return true;
        }

        Console.Write("Play a card by number (add u to call UNO, e.g. 2u), d = draw, p = pass, u = UNO, c = catch, q = quit: ");
        var input = (Console.ReadLine() ?? "q").Trim().ToLowerInvariant();
        var hand = view.Players[seat].Hand;
        switch (input)
        {
            case "q":
                return !Screen.AskYesNo("Quit this game?", false);
            case "d":
                Report(_game.Draw(seat));
                return true;
            case "p":
                Report(_game.Pass(seat));
                return true;
            case "u":
                Report(_game.CallUno(seat));
                return true;
            case "c":
                var others = view.Players.Where(p => p.Seat != seat).ToList();
                var target = Screen.Menu("Catch who?", others.Select(p => $"{p.Name} ({p.Hand.Count} cards)").ToList());
                Report(_game.CatchUno(seat, others[target].Seat));
                return true;
        }

        var callUno = input.EndsWith('u');
        if (int.TryParse(callUno ? input[..^1] : input, out var number) && number >= 1 && number <= hand.Count)
            Report(_game.Play(seat, hand[number - 1].InstanceId, callUno));
        else
            Screen.Error("Type a card number, or d, p, u, c or q.");
        return true;
    }

    private void ShowTable(GameView view, int seat)
    {
        Console.WriteLine();
        Console.Write("Top card: ");
        if (view.TopCard is { } top)
            Screen.WriteCard(top, view.TopAttributes);
        var chosen = view.TopCard is null ? [] : view.TopAttributes.Where(a => !view.TopCard.Attributes.ContainsKey(a.Key)).ToList();
        if (chosen.Count > 0)
            Console.Write($"  (chosen {string.Join(", ", chosen.Select(a => $"{a.Key}: {a.Value}"))})");
        Console.WriteLine();

        var others = view.Players.Where(p => p.Seat != seat)
            .Select(p => $"{p.Name}: {p.Hand.Count}{(p.Hand.Count == 1 && p.CalledUno ? " UNO" : "")}");
        Screen.Dim($"Draw pile {view.DrawPileCount} · {(view.Clockwise ? "clockwise" : "counter-clockwise")} · {string.Join(" · ", others)}");
        if (view.PendingDraw > 0)
            Screen.WriteLine($"You must stack a draw card or draw {view.PendingDraw}.", ConsoleColor.Yellow);

        var me = view.Players[seat];
        Console.WriteLine($"{me.Name}'s hand{(_game.Settings.Scoring == Core.Definitions.ScoringMode.TargetScore ? $" (score {me.Score})" : "")}:");
        for (var i = 0; i < me.Hand.Count; i++)
        {
            var card = me.Hand[i];
            var playable = view.PendingChoice is null && _game.CanPlay(seat, card.InstanceId);
            Console.Write(playable ? "  ▶ " : "    ");
            Console.Write($"{i + 1,2}. ");
            Screen.WriteCard(card);
            Console.WriteLine();
        }
    }

    private static void Report(CommandResult result)
    {
        if (!result.Succeeded)
            Screen.Error(result.Error!);
    }
}
