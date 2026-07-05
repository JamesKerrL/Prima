namespace Prima.App.Commands;

public sealed class CommandRegistry
{
    private readonly List<CommandDescriptor> _commands = [];

    public void Register(CommandDescriptor descriptor)
    {
        _commands.Add(descriptor);
    }

    public IReadOnlyList<CommandDescriptor> All => _commands;

    public IReadOnlyList<CommandMatch> Search(string query, int max = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _commands
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Title)
                .Take(max)
                .Select(c => new CommandMatch(c, 0))
                .ToList();

        var scored = new List<(CommandMatch Match, int Score)>();

        foreach (var cmd in _commands)
        {
            int? best = null;

            var titleScore = FuzzyMatcher.Score(cmd.Title, query);
            if (titleScore.HasValue && (!best.HasValue || titleScore.Value > best.Value))
                best = titleScore.Value;

            foreach (var kw in cmd.Keywords)
            {
                var kwScore = FuzzyMatcher.Score(kw, query);
                if (kwScore.HasValue && (!best.HasValue || kwScore.Value > best.Value))
                    best = kwScore.Value;
            }

            var catScore = FuzzyMatcher.Score(cmd.Category, query);
            if (catScore.HasValue && (!best.HasValue || catScore.Value > best.Value))
                best = catScore.Value;

            if (best.HasValue)
                scored.Add((new CommandMatch(cmd, best.Value), best.Value));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Match.Command.Category)
            .ThenBy(s => s.Match.Command.Title)
            .Take(max)
            .Select(s => s.Match)
            .ToList();
    }
}
