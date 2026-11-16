// Best scores per mini-game, kept in the PAWcare save file next to the coins.
public static class MiniGameScores
{
    public static int Best(string gameId)
    {
        int i = SaveSystem.Data.miniGameIds.IndexOf(gameId);
        return i >= 0 && i < SaveSystem.Data.miniGameBest.Count ? SaveSystem.Data.miniGameBest[i] : 0;
    }

    // Stores the score if it beats the best; returns true when it's a new best.
    public static bool Submit(string gameId, int score)
    {
        SaveData d = SaveSystem.Data;
        while (d.miniGameBest.Count < d.miniGameIds.Count)
            d.miniGameBest.Add(0);

        int i = d.miniGameIds.IndexOf(gameId);
        if (i < 0)
        {
            d.miniGameIds.Add(gameId);
            d.miniGameBest.Add(0);
            i = d.miniGameIds.Count - 1;
        }
        if (score <= d.miniGameBest[i])
            return false;
        d.miniGameBest[i] = score;
        SaveSystem.MarkDirty();
        return true;
    }
}
