using Avalonia.Controls;

namespace Prima.Desktop.Commands;

public sealed class CommandTarget
{
    public required string Id { get; init; }
    public required Func<Control?> Locate { get; init; }
    public Func<Task>? Reveal { get; init; }
}

public sealed class CommandTargetRegistry
{
    private readonly Dictionary<string, CommandTarget> _targets = [];

    public void Register(CommandTarget target)
    {
        _targets[target.Id] = target;
    }

    public CommandTarget? Get(string id)
    {
        _targets.TryGetValue(id, out var target);
        return target;
    }
}
