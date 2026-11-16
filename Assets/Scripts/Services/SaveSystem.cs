using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Everything PAWcare remembers between sessions. Add new fields with
// sensible defaults; old save files simply load them as the default.
[Serializable]
public class SaveData
{
    public int version = SaveSystem.CurrentVersion;
    public string savedAt;

    [Header("Pet")]
    public bool hasPet;
    public string petId;          // breed id from PetStageLibrary, e.g. "Beagle"
    public bool isCat;
    public string petName;        // chosen by the player; older saves hold the breed name
    public int growthStage;       // 0 Baby, 1 Young, 2 Adult
    public int growthPoints;      // version 2 only (per-stage growth); see careTotal
    public int careTotal;         // Care Points earned by this pet; never reset when it evolves
    public bool hasTrait;
    public int trait;             // Trait enum (Playful, Lazy, Foodie, Clingy, Shy)

    [Header("Needs (0-100)")]
    public bool hasNeeds;
    public long needsSavedTicks;  // UTC time the needs were stored, for decay while away
    public float hunger;
    public float thirst;
    public float cleanliness;
    public float energy;
    public float happiness;

    [Header("Progress")]
    public int carePoints;        // version 1 only; turned into coins when loaded
    public int coins;
    public int bondLevel = 1;
    public int bondXP;

    [Header("Health")]
    public int illness;           // Illness enum (None, Cold, Fleas, TummyAche, EarInfection)
    public long illnessSinceTicks;
    public int recoveringFrom;    // Illness treated at the vet; cleared once the after-care is done
    public int afterCareDone;     // AfterCare flags the player has done since the treatment
    public long healthyUntilTicks; // no new illness before this (right after a treatment)
    public long lastCheckupTicks;
    public int vaccinesGiven;     // one dose per growth stage
    public int vetVisits;

    [Header("Shop")]
    public List<string> ownedItems = new List<string>();
    public string accessory;      // id of the worn accessory, empty for none
    public string room;           // id of the room theme, empty for the default

    [Header("Daily reward")]
    public string dailyLastClaim; // local date (yyyy-MM-dd) of the last claim
    public int dailyNext;         // which of the 7 days is claimed next (0-6)

    [Header("Room messes")]
    public int messCount;         // messes waiting to be cleaned (0-2)
    public long nextMessTicks;    // UTC time the next mess appears

    [Header("Mini-games")]
    public List<string> miniGameIds = new List<string>();   // e.g. "catch", "match"
    public List<int> miniGameBest = new List<int>();        // best score, same order as miniGameIds

    [Header("Settings")]
    public float musicVolume = 0.8f;
    public float sfxVolume = 0.8f;
    public bool reduceMotion;
}

// Saves the game as a small JSON file in Application.persistentDataPath,
// which is the app's private storage on Android. Writes go to a temporary
// file first, so a crash mid-write can't destroy the last good save.
public static class SaveSystem
{
    // 2: care points became coins; added Bond, the shop and the daily reward.
    // 3: three evolutions driven by a running Care Points total (careTotal).
    public const int CurrentVersion = 3;
    const string FileName = "pawcare_save.json";

    static SaveData data;
    static bool dirty;

    // Raised just before the file is written, so components can copy their
    // live values into the save data first (PetNeeds does). Callers of Save()
    // don't need to know which components hold unsaved state.
    public static event Action BeforeSave;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // Start every launch from the file on disk (also correct when the
    // editor's "reload domain" option is turned off).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLaunch()
    {
        data = null;
        dirty = false;
        BeforeSave = null;
    }

    public static SaveData Data
    {
        get
        {
            if (data == null)
                Load();
            return data;
        }
    }

    public static void MarkDirty()
    {
        dirty = true;
    }

    public static void SaveIfDirty()
    {
        if (dirty)
            Save();
    }

    public static void Save()
    {
        if (data == null)
            return;

        BeforeSave?.Invoke();
        data.version = CurrentVersion;
        data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string tmp = FilePath + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (File.Exists(FilePath))
                File.Delete(FilePath);
            File.Move(tmp, FilePath);
            dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError("SaveSystem: could not write save file: " + e.Message);
        }
    }

    public static void Load()
    {
        data = null;
        bool imported = false;
        if (File.Exists(FilePath))
        {
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("SaveSystem: save file is damaged (" + e.Message + "). Starting fresh; a copy was kept as .corrupt.");
            }

            if (data == null)
                KeepCorruptCopy();
        }
        else
        {
            data = ImportFromPlayerPrefs();
            imported = data != null;
        }

        if (data == null)
            data = new SaveData();
        bool upgraded = Upgrade(data);
        Validate(data);
        dirty = false;
        if (imported || upgraded)
            Save(); // write the save file once so the import/upgrade only happens the first time
    }

    // Adopting a new pet: forget the old pet, its growth, Bond and health. The settings,
    // coins, bought items, room and daily-reward streak belong to the player and stay.
    public static void StartNewGame()
    {
        SaveData old = Data;
        SaveData fresh = new SaveData
        {
            musicVolume = old.musicVolume,
            sfxVolume = old.sfxVolume,
            reduceMotion = old.reduceMotion,
            coins = old.coins,
            ownedItems = new List<string>(old.ownedItems),
            room = old.room,
            dailyLastClaim = old.dailyLastClaim,
            dailyNext = old.dailyNext,
        };
        data = fresh;
        Save();
    }

    // "Reset Save" in Settings: removes all progress, keeps the sound and motion settings.
    public static void ResetProgress()
    {
        SaveData old = Data;
        data = new SaveData
        {
            musicVolume = old.musicVolume,
            sfxVolume = old.sfxVolume,
            reduceMotion = old.reduceMotion,
        };
        Save();
    }

    // Used by tests.
    public static void DeleteSave()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
        data = new SaveData();
        dirty = false;
    }

    // Brings older save files up to date. Returns true when something changed.
    static bool Upgrade(SaveData d)
    {
        if (d.version >= CurrentVersion)
            return false;
        if (d.version < 2)
        {
            // Care points became coins, one for one.
            d.coins += d.carePoints;
            d.carePoints = 0;
        }
        if (d.version < 3 && d.hasPet)
        {
            // Four stages with per-stage points (60 / 120 / 180) became three
            // evolutions on one Care Points total. Keep the pet's progress:
            // a Baby part-way to Young stays part-way, Adult and Senior become Adult.
            int stage = Mathf.Max(0, d.growthStage);
            int points = Mathf.Max(0, d.growthPoints);
            if (stage == 0)
                d.careTotal = Mathf.Min(199, points * 200 / 60);
            else if (stage == 1)
                d.careTotal = 200 + Mathf.Min(299, points * 300 / 120);
            else
                d.careTotal = 500;
            d.growthStage = Mathf.Min(stage, 2);
        }
        d.version = CurrentVersion;
        return true;
    }

    // Limits for values read from the save file. The file is plain JSON on the
    // device, so it can be damaged or edited by hand; nothing in it is trusted.
    public const int MaxPetNameLength = 12;   // same limit as the name popup
    public const int MaxCareTotal = 99999;    // Adult needs 500
    public const int MaxCoins = 999999;
    public const int MaxMiniGameScore = 9999999;

    static void Validate(SaveData d)
    {
        d.petName = CleanName(d.petName);
        d.growthPoints = Mathf.Max(0, d.growthPoints);
        d.careTotal = Mathf.Clamp(d.careTotal, 0, MaxCareTotal);
        // The stage can never be ahead of the Care Points that earn it.
        d.growthStage = Mathf.Clamp(d.growthStage, 0, CarePoints.StageFor(d.careTotal));

        // Mini-game scores: two lists of the same length, no missing ids, sensible scores.
        if (d.miniGameIds == null)
            d.miniGameIds = new List<string>();
        if (d.miniGameBest == null)
            d.miniGameBest = new List<int>();
        for (int i = d.miniGameIds.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrEmpty(d.miniGameIds[i]))
            {
                d.miniGameIds.RemoveAt(i);
                if (i < d.miniGameBest.Count)
                    d.miniGameBest.RemoveAt(i);
            }
        }
        while (d.miniGameBest.Count < d.miniGameIds.Count)
            d.miniGameBest.Add(0);
        if (d.miniGameBest.Count > d.miniGameIds.Count)
            d.miniGameBest.RemoveRange(d.miniGameIds.Count, d.miniGameBest.Count - d.miniGameIds.Count);
        for (int i = 0; i < d.miniGameBest.Count; i++)
            d.miniGameBest[i] = Mathf.Clamp(d.miniGameBest[i], 0, MaxMiniGameScore);

        d.messCount = Mathf.Clamp(d.messCount, 0, 2);
        if (d.nextMessTicks < 0 || d.nextMessTicks > DateTime.MaxValue.Ticks)
            d.nextMessTicks = 0;
        d.carePoints = Mathf.Max(0, d.carePoints);
        d.coins = Mathf.Clamp(d.coins, 0, MaxCoins);
        d.bondLevel = Mathf.Clamp(d.bondLevel, 1, 99);
        d.bondXP = Mathf.Max(0, d.bondXP);
        if (d.ownedItems == null)
            d.ownedItems = new List<string>();
        d.dailyNext = Mathf.Clamp(d.dailyNext, 0, 6);
        d.hunger = Mathf.Clamp(d.hunger, 0f, 100f);
        d.thirst = Mathf.Clamp(d.thirst, 0f, 100f);
        d.cleanliness = Mathf.Clamp(d.cleanliness, 0f, 100f);
        d.energy = Mathf.Clamp(d.energy, 0f, 100f);
        d.happiness = Mathf.Clamp(d.happiness, 0f, 100f);
        d.musicVolume = Mathf.Clamp01(d.musicVolume);
        d.sfxVolume = Mathf.Clamp01(d.sfxVolume);
        d.trait = Mathf.Clamp(d.trait, 0, 4);
        d.illness = Mathf.Clamp(d.illness, 0, 4);
        d.recoveringFrom = Mathf.Clamp(d.recoveringFrom, 0, 4);
        d.vaccinesGiven = Mathf.Max(0, d.vaccinesGiven);
        d.vetVisits = Mathf.Max(0, d.vetVisits);
        if (d.illnessSinceTicks < 0 || d.illnessSinceTicks > DateTime.MaxValue.Ticks)
            d.illnessSinceTicks = 0;
        if (d.healthyUntilTicks < 0 || d.healthyUntilTicks > DateTime.MaxValue.Ticks)
            d.healthyUntilTicks = 0;
        if (d.lastCheckupTicks < 0 || d.lastCheckupTicks > DateTime.MaxValue.Ticks)
            d.lastCheckupTicks = 0;
        if (d.needsSavedTicks < 0 || d.needsSavedTicks > DateTime.MaxValue.Ticks)
            d.needsSavedTicks = 0;
        if (d.hasPet && string.IsNullOrEmpty(d.petId))
            d.hasPet = false;
    }

    // A pet name from the save file gets the same cleaning as one typed into the
    // name popup: no rich-text tags, no surrounding spaces, limited length.
    static string CleanName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        var sb = new System.Text.StringBuilder();
        bool inTag = false;
        foreach (char c in raw)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag && !char.IsControl(c)) sb.Append(c);
        }
        string s = sb.ToString().Trim();
        return s.Length > MaxPetNameLength ? s.Substring(0, MaxPetNameLength).Trim() : s;
    }

    static void KeepCorruptCopy()
    {
        try
        {
            File.Copy(FilePath, FilePath + ".corrupt", true);
        }
        catch (Exception)
        {
            // Not being able to keep a copy shouldn't stop the game.
        }
    }

    // One-time import of progress saved by earlier versions of the game (PlayerPrefs).
    static SaveData ImportFromPlayerPrefs()
    {
        bool anything = PlayerPrefs.HasKey("CurrentPet") || PlayerPrefs.HasKey("CarePoints")
            || PlayerPrefs.HasKey("MusicVolume") || PlayerPrefs.HasKey("PetNeeds_Hunger");
        if (!anything)
            return null;

        SaveData d = new SaveData
        {
            // Old format, so Upgrade() turns the old growth points into a Care Points
            // total that matches the pet's stage (Validate would otherwise lower the stage).
            version = 2,
            coins = PlayerPrefs.GetInt("CarePoints", 0),
            musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.8f),
            sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 0.8f),
        };

        string pet = PlayerPrefs.GetString("CurrentPet", "");
        if (!string.IsNullOrEmpty(pet))
        {
            d.hasPet = true;
            d.petId = pet;
            d.growthStage = PlayerPrefs.GetInt("Growth_" + pet + "_Stage", 0);
            d.growthPoints = PlayerPrefs.GetInt("Growth_" + pet + "_Points", 0);
        }

        if (PlayerPrefs.HasKey("PetNeeds_Hunger"))
        {
            d.hasNeeds = true;
            d.hunger = PlayerPrefs.GetFloat("PetNeeds_Hunger");
            d.thirst = PlayerPrefs.GetFloat("PetNeeds_Thirst");
            d.cleanliness = PlayerPrefs.GetFloat("PetNeeds_Cleanliness");
            d.energy = PlayerPrefs.GetFloat("PetNeeds_Energy");
            d.happiness = PlayerPrefs.GetFloat("PetNeeds_Happiness");
        }

        Debug.Log("SaveSystem: imported earlier progress from PlayerPrefs.");
        return d;
    }
}
