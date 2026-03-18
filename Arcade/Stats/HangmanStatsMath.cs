namespace Arcade.Stats;

internal static class HangmanStatsMath
{
    public static double? GetAverageWrongGuessesPerRound(HangmanAccountStatsData stats)
    {
        return stats.RoundsPlayed > 0
            ? (double)stats.TotalWrongGuesses / stats.RoundsPlayed
            : null;
    }

    public static double? GetAverageWrongGuessesOnWins(HangmanAccountStatsData stats)
    {
        return stats.Wins > 0
            ? (double)stats.TotalWrongGuessesOnWins / stats.Wins
            : null;
    }
}
