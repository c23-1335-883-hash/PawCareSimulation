using System;
using UnityEngine;

// Care Points: the one number that grows the pet. They are earned only by
// finished care (see the callers) and never reset when the pet evolves:
//   Evolution 1 - Baby:   0 points
//   Evolution 2 - Young:  200 points
//   Evolution 3 - Adult:  500 points
// The total lives in the save file, so points earned in another scene
// (mini-games) are picked up by PetGrowth when home opens.
public static class CarePoints
{
    // Rewards (the callers check that the care was really done and needed).
    public const int Feed = 10;
    public const int Bath = 10;
    public const int Play = 10;          // a finished mini-game round, or fetch at home
    public const int CleanRoom = 5;
    public const int VetCheckup = 15;

    // Total points needed to reach each evolution (index = stage).
    public static readonly int[] StageStarts = { 0, 200, 500 };
    public static readonly string[] StageNames = { "Baby", "Young", "Adult" };
    public static int MaxStage => StageStarts.Length - 1;

    // amount, what it was for
    public static event Action<int, string> Earned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLaunch()
    {
        Earned = null;
    }

    public static int Total => SaveSystem.Data.careTotal;

    public static int StageFor(int total)
    {
        int stage = 0;
        for (int i = 1; i < StageStarts.Length; i++)
        {
            if (total >= StageStarts[i])
                stage = i;
        }
        return stage;
    }

    // Points the current evolution is working toward (the last one once fully grown).
    public static int GoalFor(int total)
    {
        int next = Mathf.Min(StageFor(total) + 1, MaxStage);
        return StageStarts[next];
    }

    // Adds points for a finished care action and saves straight away.
    // Returns false (and adds nothing) when there is no pet.
    public static bool Add(int amount, string reason)
    {
        if (amount <= 0 || !SaveSystem.Data.hasPet)
            return false;
        // Stops at the limit instead of overflowing.
        SaveSystem.Data.careTotal = Mathf.Min(SaveSystem.MaxCareTotal, SaveSystem.Data.careTotal + Mathf.Min(amount, SaveSystem.MaxCareTotal));
        SaveSystem.Save();
        Earned?.Invoke(amount, reason);
        return true;
    }

    // A newly adopted pet starts from zero.
    public static void ResetForNewPet()
    {
        SaveSystem.Data.careTotal = 0;
        SaveSystem.MarkDirty();
    }
}
