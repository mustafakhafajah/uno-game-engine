using UnoEngine.ConsoleApp.Ui;
using UnoEngine.Core.Definitions;
using UnoEngine.Rules.Definitions;

namespace UnoEngine.ConsoleApp;

/// <summary>Finds game definitions: the built-in presets and the games the user designed.</summary>
internal static class GameLibrary
{
    public static string PresetsFolder => Path.Combine(AppContext.BaseDirectory, "presets");

    public static string MyGamesFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UnoEngine", "games");

    public static IReadOnlyList<string> AllFiles() =>
        new[] { PresetsFolder, MyGamesFolder }
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, "*.json").Order())
            .ToList();

    /// <summary>Lets the user pick a game file (or type a path). Returns null to go back.</summary>
    public static (GameDefinition Definition, string Path)? Pick(string prompt)
    {
        var files = AllFiles();
        var options = files.Select(Describe).Append("Enter a file path").Append("Back").ToList();
        var choice = Screen.Menu(prompt, options);
        if (choice == options.Count - 1)
            return null;
        var path = choice < files.Count ? files[choice] : Screen.Ask("Path to the game JSON file");
        try
        {
            return (GameDefinitionLoader.Load(path), path);
        }
        catch (Exception ex) when (ex is DefinitionException or IOException or UnauthorizedAccessException)
        {
            Screen.Error(ex.Message);
            return null;
        }
    }

    private static string Describe(string file)
    {
        var where = file.StartsWith(PresetsFolder, StringComparison.OrdinalIgnoreCase) ? "built-in" : "yours";
        try
        {
            var definition = GameDefinitionLoader.Load(file);
            return $"{definition.Name} ({definition.DeckSize} cards, {definition.MinPlayers}-{definition.MaxPlayers} players, {where})";
        }
        catch (DefinitionException)
        {
            return $"{Path.GetFileName(file)} (cannot be read, {where})";
        }
    }
}
