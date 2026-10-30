using UnoEngine.Engine.Mediator;

namespace UnoEngine.Engine.Commands;

/// <summary>Something a player (or the host) asks the game to do (Command pattern).</summary>
public interface IGameCommand
{
    void Execute(GameMediator game);
}

public sealed record StartGameCommand : IGameCommand
{
    public void Execute(GameMediator game) => game.HandleStart();
}

public sealed record StartNextRoundCommand : IGameCommand
{
    public void Execute(GameMediator game) => game.HandleStartNextRound();
}

/// <param name="CallUno">Call UNO as part of the play (when this leaves one card).</param>
public sealed record PlayCardCommand(int Seat, int CardId, bool CallUno = false) : IGameCommand
{
    public void Execute(GameMediator game) => game.HandlePlay(Seat, CardId, CallUno);
}

public sealed record DrawCardCommand(int Seat) : IGameCommand
{
    public void Execute(GameMediator game) => game.HandleDraw(Seat);
}

public sealed record PassCommand(int Seat) : IGameCommand
{
    public void Execute(GameMediator game) => game.HandlePass(Seat);
}

public sealed record ChooseCommand(int Seat, string Option) : IGameCommand
{
    public void Execute(GameMediator game) => game.HandleChoose(Seat, Option);
}

public sealed record CallUnoCommand(int Seat) : IGameCommand
{
    public void Execute(GameMediator game) => game.HandleCallUno(Seat);
}

public sealed record CatchUnoCommand(int CatcherSeat, int TargetSeat) : IGameCommand
{
    public void Execute(GameMediator game) => game.HandleCatchUno(CatcherSeat, TargetSeat);
}

public sealed record CommandResult(bool Succeeded, string? Error)
{
    public static CommandResult Ok { get; } = new(true, null);

    public static CommandResult Fail(string error) => new(false, error);
}
