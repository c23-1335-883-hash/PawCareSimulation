using System;
using UnityEngine;

public enum Need
{
    Hunger,
    Thirst,
    Cleanliness,
    Energy,
    Happiness
}

// Holds the pet's five needs (0 = neglected, 100 = fully cared for) and lets
// them drop slowly over time. All tuning values are exposed in the Inspector.
// Values are kept in the save file (SaveSystem) so the pet remembers its state between
// sessions. Time away also counts, but gently: slower, capped, and never below a floor.
public class PetNeeds : MonoBehaviour
{
    [Serializable]
    public class NeedSettings
    {
        public Need need;
        public string label;
        [Range(0f, 100f)] public float startValue = 80f;
        [Tooltip("Points lost per minute of play.")]
        public float decayPerMinute = 2f;
        [Tooltip("Below this the meter shows a warning and the pet asks for help.")]
        public float lowBelow = 35f;
        [Tooltip("Below this the warning becomes urgent.")]
        public float urgentBelow = 15f;
    }

    public NeedSettings[] needs =
    {
        new NeedSettings { need = Need.Hunger,      label = "Food",   startValue = 60f, decayPerMinute = 3f,   lowBelow = 35f, urgentBelow = 15f },
        new NeedSettings { need = Need.Thirst,      label = "Water",  startValue = 55f, decayPerMinute = 4f,   lowBelow = 35f, urgentBelow = 15f },
        new NeedSettings { need = Need.Cleanliness, label = "Clean",  startValue = 65f, decayPerMinute = 2f,   lowBelow = 35f, urgentBelow = 15f },
        new NeedSettings { need = Need.Energy,      label = "Energy", startValue = 85f, decayPerMinute = 1.5f, lowBelow = 25f, urgentBelow = 10f },
        new NeedSettings { need = Need.Happiness,   label = "Mood",   startValue = 75f, decayPerMinute = 1f,   lowBelow = 35f, urgentBelow = 15f },
    };

    [Header("Balance")]
    [Tooltip("Speeds up or slows down every need. Use a higher value for demos.")]
    public float decaySpeed = 1f;
    [Tooltip("Extra mood lost per minute while another need is urgent.")]
    public float neglectMoodPerMinute = 2f;
    public bool saveProgress = true;

    [Header("While the game is closed")]
    [Tooltip("Needs drop this much slower while away (0.1 = a tenth of the in-game speed).")]
    public float awaySpeed = 0.1f;
    [Tooltip("Time away counts up to this many hours.")]
    public float awayMaxHours = 8f;
    [Tooltip("Time away never pushes a need below this.")]
    public float awayFloor = 25f;
    [Tooltip("The pet naps while you're away, so energy goes up by this much per hour.")]
    public float awayEnergyPerHour = 25f;

    public event Action Changed;

    // Minutes since the needs were last saved (0 for a new pet).
    public float MinutesAway { get; private set; }

    const float TickSeconds = 1f;

    float[] values;
    float tickTimer;

    void Awake()
    {
        SaveData save = SaveSystem.Data;
        bool restore = saveProgress && save.hasNeeds;
        if (restore && save.needsSavedTicks > 0)
        {
            double minutes = (DateTime.UtcNow - new DateTime(save.needsSavedTicks, DateTimeKind.Utc)).TotalMinutes;
            MinutesAway = Mathf.Max(0f, (float)minutes);
        }

        float counted = Mathf.Min(MinutesAway, awayMaxHours * 60f);
        float awayMinutes = counted * awaySpeed * decaySpeed;
        values = new float[needs.Length];
        for (int i = 0; i < needs.Length; i++)
        {
            float v = needs[i].startValue;
            if (restore)
            {
                float saved = Saved(save, needs[i].need);
                if (needs[i].need == Need.Energy)
                    v = saved + awayEnergyPerHour * counted / 60f;
                else
                    v = Mathf.Max(saved - needs[i].decayPerMinute * awayMinutes, Mathf.Min(saved, awayFloor));
            }
            values[i] = Mathf.Clamp(v, 0f, 100f);
        }
    }

    void Update()
    {
        tickTimer += Time.deltaTime;
        if (tickTimer < TickSeconds)
            return;

        float minutes = tickTimer / 60f * decaySpeed;
        tickTimer = 0f;

        bool neglected = false;
        for (int i = 0; i < needs.Length; i++)
        {
            values[i] = Mathf.Max(0f, values[i] - needs[i].decayPerMinute * minutes);
            if (needs[i].need != Need.Happiness && IsUrgent(needs[i].need))
                neglected = true;
        }
        if (neglected)
            Change(Need.Happiness, -neglectMoodPerMinute * minutes, false);

        Changed?.Invoke();
    }

    public float Get(Need need)
    {
        return values[Index(need)];
    }

    public void Add(Need need, float amount)
    {
        Change(need, amount, true);
    }

    public string Label(Need need)
    {
        return needs[Index(need)].label;
    }

    public bool IsLow(Need need)
    {
        return Get(need) < needs[Index(need)].lowBelow;
    }

    public bool IsUrgent(Need need)
    {
        return Get(need) < needs[Index(need)].urgentBelow;
    }

    // The low need with the smallest value, or null when everything is fine.
    public Need? MostPressing()
    {
        Need? result = null;
        float lowest = float.MaxValue;
        foreach (NeedSettings s in needs)
        {
            float v = Get(s.need);
            if (v < s.lowBelow && v < lowest)
            {
                lowest = v;
                result = s.need;
            }
        }
        return result;
    }

    void Change(Need need, float amount, bool notify)
    {
        int i = Index(need);
        values[i] = Mathf.Clamp(values[i] + amount, 0f, 100f);
        if (notify)
            Changed?.Invoke();
    }

    int Index(Need need)
    {
        for (int i = 0; i < needs.Length; i++)
        {
            if (needs[i].need == need)
                return i;
        }
        throw new ArgumentException("PetNeeds has no settings for " + need);
    }

    // Copies the current values into the save data (written to disk at the next save).
    public void Store()
    {
        if (!saveProgress || values == null)
            return;
        SaveData save = SaveSystem.Data;
        save.hasNeeds = true;
        save.hunger = Get(Need.Hunger);
        save.thirst = Get(Need.Thirst);
        save.cleanliness = Get(Need.Cleanliness);
        save.energy = Get(Need.Energy);
        save.happiness = Get(Need.Happiness);
        save.needsSavedTicks = DateTime.UtcNow.Ticks;
        SaveSystem.MarkDirty();
    }

    static float Saved(SaveData save, Need need)
    {
        switch (need)
        {
            case Need.Hunger: return save.hunger;
            case Need.Thirst: return save.thirst;
            case Need.Cleanliness: return save.cleanliness;
            case Need.Energy: return save.energy;
            default: return save.happiness;
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
            return;
        Store();
        SaveSystem.SaveIfDirty();
    }

    // Any save in the scene (growth, shop, care...) also writes the current needs.
    void OnEnable()
    {
        SaveSystem.BeforeSave += Store;
    }

    void OnDisable()
    {
        Store();
        SaveSystem.SaveIfDirty();
        SaveSystem.BeforeSave -= Store;
    }
}
