namespace UnoEngine.Rules.Definitions;

/// <summary>A game definition (or one of its abilities) is not valid.</summary>
public sealed class DefinitionException(string message) : Exception(message)
{
    public DefinitionException(IReadOnlyList<string> errors)
        : this("The game definition is not valid:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => " - " + e)))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; } = [];
}
