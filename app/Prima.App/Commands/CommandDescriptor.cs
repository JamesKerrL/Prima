namespace Prima.App.Commands;

public sealed record CommandDescriptor(
    string Id,
    string Title,
    string Category,
    IReadOnlyList<string> Keywords,
    string? Shortcut = null);

public sealed record CommandMatch(CommandDescriptor Command, int Score);
