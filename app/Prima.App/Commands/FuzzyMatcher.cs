namespace Prima.App.Commands;

public static class FuzzyMatcher
{
    public static int? Score(string candidate, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 0;

        int ci = 0, qi = 0;
        int score = 0;
        bool prevMatched = false;

        while (ci < candidate.Length && qi < query.Length)
        {
            if (char.ToUpperInvariant(candidate[ci]) == char.ToUpperInvariant(query[qi]))
            {
                if (prevMatched)
                    score += 5;
                else if (ci == 0 || candidate[ci - 1] == ' ' || candidate[ci - 1] == '.' || candidate[ci - 1] == '-')
                    score += 3;

                score += 1;
                prevMatched = true;
                qi++;
            }
            else
            {
                prevMatched = false;
            }

            ci++;
        }

        return qi == query.Length ? score : null;
    }
}
