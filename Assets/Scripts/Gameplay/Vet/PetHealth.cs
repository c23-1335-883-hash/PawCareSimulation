using System;
using UnityEngine;

public enum Illness
{
    None,
    Cold,
    Fleas,
    TummyAche,
    EarInfection
}

// Care the vet asks for after a treatment; the pet is fully better once it's all done.
[Flags]
public enum AfterCare
{
    Feed = 1,
    Water = 2,
    Bath = 4,
    Brush = 8,
    Rest = 16
}

// The pet's health. Neglect can make it sick: a dirty pet can pick up fleas or
// an ear infection, skipped meals or water give it a tummy ache, and a tired,
// unhappy pet can catch a cold. Vaccines lower the chance. A sick pet shows
// symptoms, loses mood faster and won't play until the vet treats it. After the
// treatment the vet gives after-care to do at home, and the pet is fully better
// once the player has done it. Nothing here is permanent or fatal.
public class PetHealth : MonoBehaviour
{
    [Header("References")]
    public PetNeeds needs;
    public PetController pet;
    public PetCareManager care;
    public PetBond bond;
    public VetClinic clinic;

    [Header("Getting sick")]
    public float checkEverySeconds = 60f;
    [Tooltip("Chance per check when a related need is low.")]
    public float lowChance = 0.05f;
    [Tooltip("Chance per check when a related need is urgent.")]
    public float urgentChance = 0.18f;
    [Tooltip("Each vaccine dose lowers the chance by this share.")]
    public float protectionPerDose = 0.2f;
    [Tooltip("Minutes with no new illness after a treatment.")]
    public float healthyMinutesAfterTreatment = 30f;
    [Tooltip("Chance per hour away of coming back to a sick pet, when a need is low (capped below).")]
    public float awayChancePerHour = 0.1f;
    public float awayMaxChance = 0.4f;

    [Header("While sick")]
    [Tooltip("Extra mood lost per minute while sick.")]
    public float sickMoodPerMinute = 1.5f;
    public Vector2 symptomInterval = new Vector2(12f, 20f);
    [Tooltip("Seconds between banner reminders to visit the vet.")]
    public float reminderInterval = 75f;

    [Header("Vet")]
    public float checkupCooldownMinutes = 15f;
    public int maxVaccines = 3;

    [Header("Getting better")]
    public int recoveryHearts = 4;
    public float recoveryHappiness = 10f;

    public event Action Changed;

    public Illness Current => (Illness)SaveSystem.Data.illness;
    public bool IsSick => Current != Illness.None;
    public Illness RecoveringFrom => (Illness)SaveSystem.Data.recoveringFrom;
    public bool IsRecovering => RecoveringFrom != Illness.None;
    public int Vaccines => SaveSystem.Data.vaccinesGiven;
    // One dose when adopted, then one more each time the pet grows up a stage.
    public bool VaccineDue => Vaccines < maxVaccines && Vaccines <= SaveSystem.Data.growthStage;
    public bool CheckupDue => MinutesSince(SaveSystem.Data.lastCheckupTicks) >= checkupCooldownMinutes;
    public float MinutesSinceCheckup => MinutesSince(SaveSystem.Data.lastCheckupTicks);
    public AfterCare AfterCareLeft => Steps(RecoveringFrom) & ~(AfterCare)SaveSystem.Data.afterCareDone;

    float Protection => Mathf.Clamp01(1f - protectionPerDose * Vaccines);
    bool InClinic => clinic != null && clinic.IsOpen;

    float checkTimer;
    float moodTimer;
    float nextSymptom;
    float nextReminder;

    void Awake()
    {
        if (clinic == null)
            clinic = GetComponent<VetClinic>();
    }

    void OnEnable()
    {
        if (care != null)
        {
            care.ActivityCompleted += OnActivity;
            care.Rested += OnRested;
        }
    }

    void OnDisable()
    {
        if (care != null)
        {
            care.ActivityCompleted -= OnActivity;
            care.Rested -= OnRested;
        }
    }

    void Start()
    {
        CheckWhileAway();
        nextSymptom = Time.time + 5f;
        nextReminder = Time.time + 9f;
        if (pet != null)
            pet.Illness = Current;
    }

    void Update()
    {
        if (needs == null || pet == null)
            return;

        checkTimer += Time.deltaTime;
        if (checkTimer >= checkEverySeconds)
        {
            checkTimer = 0f;
            RollForIllness();
        }

        if (!IsSick || InClinic)
            return;

        moodTimer += Time.deltaTime;
        if (moodTimer >= 1f)
        {
            needs.Add(Need.Happiness, -sickMoodPerMinute * moodTimer / 60f * needs.decaySpeed);
            moodTimer = 0f;
        }
        if (Time.time >= nextSymptom && pet.IsIdle)
        {
            pet.ShowSymptom(Current);
            nextSymptom = Time.time + UnityEngine.Random.Range(symptomInterval.x, symptomInterval.y);
        }
        if (Time.time >= nextReminder && care != null && !care.IsResting)
        {
            Nudge();
            nextReminder = Time.time + reminderInterval;
        }
    }

    // ---- Called by the vet ----

    public void MakeSick(Illness illness)
    {
        if (illness == Illness.None)
            return;
        SaveData save = SaveSystem.Data;
        save.illness = (int)illness;
        save.illnessSinceTicks = DateTime.UtcNow.Ticks;
        save.recoveringFrom = 0;
        save.afterCareDone = 0;
        Commit();
        pet.ShowSymptom(illness);
        nextSymptom = Time.time + UnityEngine.Random.Range(symptomInterval.x, symptomInterval.y);
        nextReminder = Time.time + 4f;
    }

    // The vet has treated the illness; the after-care at home finishes the job.
    public void Treat()
    {
        if (!IsSick)
            return;
        SaveData save = SaveSystem.Data;
        save.recoveringFrom = save.illness;
        save.illness = 0;
        save.afterCareDone = 0;
        save.healthyUntilTicks = DateTime.UtcNow.AddMinutes(healthyMinutesAfterTreatment).Ticks;
        Commit();
    }

    public void GiveVaccine()
    {
        SaveSystem.Data.vaccinesGiven++;
        Commit();
    }

    public void RecordCheckup()
    {
        SaveSystem.Data.lastCheckupTicks = DateTime.UtcNow.Ticks;
        Commit();
    }

    public void RecordVisit()
    {
        SaveSystem.Data.vetVisits++;
        Commit();
    }

    public static AfterCare Steps(Illness illness)
    {
        switch (illness)
        {
            case Illness.Cold: return AfterCare.Rest | AfterCare.Water;
            case Illness.Fleas: return AfterCare.Bath | AfterCare.Brush;
            case Illness.TummyAche: return AfterCare.Feed | AfterCare.Water;
            case Illness.EarInfection: return AfterCare.Brush | AfterCare.Rest;
            default: return 0;
        }
    }

    // ---- Getting sick ----

    void RollForIllness()
    {
        if (IsSick || InClinic || (care != null && care.IsResting))
            return;
        if (DateTime.UtcNow.Ticks < SaveSystem.Data.healthyUntilTicks)
            return;

        Illness dirty = UnityEngine.Random.value < 0.7f ? Illness.Fleas : Illness.EarInfection;
        if (Roll(Chance(Need.Cleanliness))) MakeSick(dirty);
        else if (Roll(Chance(Need.Hunger, Need.Thirst))) MakeSick(Illness.TummyAche);
        else if (Roll(ColdChance())) MakeSick(Illness.Cold);
    }

    // Time away counts gently: only when a need is low, and capped.
    void CheckWhileAway()
    {
        if (IsSick || needs == null || !SaveSystem.Data.hasNeeds)
            return;
        float hours = needs.MinutesAway / 60f;
        if (hours < 1f || DateTime.UtcNow.Ticks < SaveSystem.Data.healthyUntilTicks)
            return;

        Illness pick = Illness.None;
        if (needs.IsLow(Need.Cleanliness)) pick = Illness.Fleas;
        else if (needs.IsLow(Need.Hunger) || needs.IsLow(Need.Thirst)) pick = Illness.TummyAche;
        else if (needs.IsLow(Need.Energy)) pick = Illness.Cold;
        if (pick == Illness.None)
            return;
        if (UnityEngine.Random.value < Mathf.Min(hours * awayChancePerHour, awayMaxChance) * Protection)
        {
            SaveData save = SaveSystem.Data;
            save.illness = (int)pick;
            save.illnessSinceTicks = DateTime.UtcNow.Ticks;
            Commit();
        }
    }

    bool Roll(float chance)
    {
        return chance > 0f && UnityEngine.Random.value < chance * Protection;
    }

    float Chance(params Need[] related)
    {
        float chance = 0f;
        foreach (Need n in related)
        {
            if (needs.IsUrgent(n)) chance = Mathf.Max(chance, urgentChance);
            else if (needs.IsLow(n)) chance = Mathf.Max(chance, lowChance);
        }
        return chance;
    }

    float ColdChance()
    {
        if (needs.IsLow(Need.Energy) && needs.IsLow(Need.Happiness))
            return urgentChance;
        return needs.IsUrgent(Need.Energy) ? lowChance : 0f;
    }

    // ---- After-care ----

    void OnActivity(CareActivity activity)
    {
        switch (activity)
        {
            case CareActivity.Feed: Done(AfterCare.Feed); break;
            case CareActivity.Water: Done(AfterCare.Water); break;
            case CareActivity.Clean: Done(AfterCare.Bath); break;
            case CareActivity.Groom: Done(AfterCare.Brush); break;
        }
    }

    void OnRested()
    {
        Done(AfterCare.Rest);
    }

    void Done(AfterCare step)
    {
        if (!IsRecovering || (AfterCareLeft & step) == 0)
            return;
        SaveSystem.Data.afterCareDone |= (int)step;
        Commit();
        if (AfterCareLeft == 0)
            GetBetter();
        else if (care != null)
            care.ShowTip("That helps! Still to do for your pet's recovery: " + VetDialogue.StepList(AfterCareLeft) + ".");
    }

    void GetBetter()
    {
        SaveSystem.Data.recoveringFrom = 0;
        SaveSystem.Data.afterCareDone = 0;
        Commit();
        needs.Add(Need.Happiness, recoveryHappiness);
        if (bond != null)
            bond.Add(recoveryHearts);
        pet.Celebrate(new Line("I feel all better! Thank you!", LineIcon.Heart), false, true);
        if (care != null)
            care.ShowTip("Your pet is all better! Following the vet's advice at home helps pets heal.");
    }

    // ---- Helpers ----

    void Nudge()
    {
        if (care != null)
            care.ShowTip("Your pet " + VetDialogue.SymptomHint(Current) + " Tap <b>VET</b> to visit the veterinarian.");
    }

    void Commit()
    {
        SaveSystem.MarkDirty();
        SaveSystem.SaveIfDirty();
        if (pet != null)
            pet.Illness = Current;
        Changed?.Invoke();
    }

    static float MinutesSince(long ticks)
    {
        if (ticks <= 0)
            return float.MaxValue;
        return (float)(DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalMinutes;
    }
}
