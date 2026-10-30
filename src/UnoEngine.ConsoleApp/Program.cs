using System.Text;
using UnoEngine.ConsoleApp.Design;
using UnoEngine.ConsoleApp.Play;
using UnoEngine.ConsoleApp.Ui;

Console.OutputEncoding = Encoding.UTF8;

try
{
    while (true)
    {
        Screen.Title("Uno Game Engine");
        var choice = Screen.Menu("Choose", ["Play a game", "Design your own cards", "Exit"]);
        switch (choice)
        {
            case 0:
                GameSession.SetUpAndRun();
                break;
            case 1:
                CardDesigner.Run();
                break;
            default:
                return;
        }
    }
}
catch (EndOfStreamException)
{
    // Input was closed (e.g. Ctrl+Z / piped input ended): exit quietly.
}
