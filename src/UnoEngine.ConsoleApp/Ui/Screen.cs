using UnoEngine.Core.Cards;

namespace UnoEngine.ConsoleApp.Ui;

/// <summary>Small console helpers: prompts, menus and coloured cards.</summary>
internal static class Screen
{
    public static void Title(string text)
    {
        Console.WriteLine();
        WriteColored($"== {text} ==", ConsoleColor.Cyan);
        Console.WriteLine();
    }

    public static void Info(string text) => Console.WriteLine(text);

    public static void Error(string text) => WriteLine(text, ConsoleColor.Red);

    public static void Success(string text) => WriteLine(text, ConsoleColor.Green);

    public static void Dim(string text) => WriteLine(text, ConsoleColor.DarkGray);

    public static void WriteLine(string text, ConsoleColor color)
    {
        WriteColored(text, color);
        Console.WriteLine();
    }

    public static void WriteColored(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = previous;
    }

    public static string Ask(string prompt, string? fallback = null)
    {
        while (true)
        {
            Console.Write(fallback is null ? $"{prompt}: " : $"{prompt} [{fallback}]: ");
            var input = Console.ReadLine();
            if (input is null)
                throw new EndOfStreamException("Input closed.");
            input = input.Trim();
            if (input.Length > 0)
                return input;
            if (fallback is not null)
                return fallback;
        }
    }

    public static string? AskOptional(string prompt)
    {
        Console.Write($"{prompt} (leave empty for none): ");
        var input = Console.ReadLine() ?? throw new EndOfStreamException("Input closed.");
        return string.IsNullOrWhiteSpace(input) ? null : input.Trim();
    }

    public static int AskInt(string prompt, int min, int max, int? fallback = null)
    {
        while (true)
        {
            var input = Ask($"{prompt} ({min}-{max})", fallback?.ToString());
            if (int.TryParse(input, out var value) && value >= min && value <= max)
                return value;
            Error($"Enter a number from {min} to {max}.");
        }
    }

    public static bool AskYesNo(string prompt, bool fallback)
    {
        while (true)
        {
            var input = Ask($"{prompt} (y/n)", fallback ? "y" : "n").ToLowerInvariant();
            if (input is "y" or "yes")
                return true;
            if (input is "n" or "no")
                return false;
            Error("Answer y or n.");
        }
    }

    /// <summary>Shows a numbered menu and returns the chosen index.</summary>
    public static int Menu(string prompt, IReadOnlyList<string> options, int? fallback = null)
    {
        for (var i = 0; i < options.Count; i++)
            Console.WriteLine($"  {i + 1}. {options[i]}");
        return AskInt(prompt, 1, options.Count, fallback + 1) - 1;
    }

    public static void Pause(string message = "Press Enter to continue")
    {
        Console.Write($"{message}...");
        Console.ReadLine();
    }

    public static void Clear()
    {
        try
        {
            if (!Console.IsOutputRedirected)
                Console.Clear();
        }
        catch (IOException)
        {
            // No real console attached; keep scrolling instead.
        }
    }

    /// <summary>"Skip · red": the label plus any attribute values that add information.</summary>
    public static string Describe(Card card)
    {
        var extra = card.Attributes.Values
            .Where(v => !string.Equals(v, card.Label, StringComparison.OrdinalIgnoreCase))
            .Where(v => !card.IsWild || !v.Contains("wild", StringComparison.OrdinalIgnoreCase));
        var text = string.Join(" · ", new[] { card.Label }.Concat(extra));
        return card.Definition.Abilities.Count == 0 ? text : $"{text} *";
    }

    public static void WriteCard(Card card, IReadOnlyDictionary<string, string>? attributes = null)
    {
        attributes ??= card.Attributes;
        var color = attributes.Values
            .Select(v => Enum.TryParse<ConsoleColor>(v, ignoreCase: true, out var c) ? c : (ConsoleColor?)null)
            .FirstOrDefault(c => c is not null) ?? (card.IsWild ? ConsoleColor.Magenta : ConsoleColor.White);
        WriteColored(Describe(card), color);
    }
}
