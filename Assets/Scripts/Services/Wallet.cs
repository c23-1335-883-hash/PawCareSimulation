using System;
using UnityEngine;

// The player's coins and bought items, kept in the save file. Coins come from
// care the pet really needed, the daily reward, Bond level-ups and growing up;
// they are spent in the Shop. Coins and items stay when a new pet is adopted.
public static class Wallet
{
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLaunch()
    {
        Changed = null;
    }

    public static int Coins => SaveSystem.Data.coins;

    public static void Add(int amount)
    {
        if (amount <= 0)
            return;
        // Stops at the limit instead of overflowing.
        SaveSystem.Data.coins = Mathf.Min(SaveSystem.MaxCoins, SaveSystem.Data.coins + Mathf.Min(amount, SaveSystem.MaxCoins));
        SaveSystem.MarkDirty();
        Changed?.Invoke();
    }

    // Takes the coins if there are enough; returns false (and changes nothing) otherwise.
    public static bool Spend(int amount)
    {
        if (amount < 0 || Coins < amount)
            return false;
        SaveSystem.Data.coins -= amount;
        SaveSystem.MarkDirty();
        Changed?.Invoke();
        return true;
    }

    public static bool Owns(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && SaveSystem.Data.ownedItems.Contains(itemId);
    }

    public static void Own(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || Owns(itemId))
            return;
        SaveSystem.Data.ownedItems.Add(itemId);
        SaveSystem.MarkDirty();
        Changed?.Invoke();
    }
}
