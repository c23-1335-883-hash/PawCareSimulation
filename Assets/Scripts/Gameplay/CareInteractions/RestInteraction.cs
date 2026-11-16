using UnityEngine;

// Bedtime: stroke the pet slowly until it settles down and falls asleep, then
// leave it in peace. Fast strokes get it excited again; poking a sleeping pet
// wakes it a little and costs energy. PetCareManager ends it when the pet wakes
// up and reads Quality for the rating.
public class RestInteraction : CareInteraction
{
    [Tooltip("Slow stroke distance (canvas units) to settle the pet down.")]
    public float settleNeeded = 700f;
    [Tooltip("Settling needs slower strokes than other care (share of the rough speed).")]
    public float settleSpeedShare = 0.5f;
    public float disturbCost = 0.2f;
    public float disturbEnergy = 5f;

    public override string Prompt => "Stroke your pet slowly to help it settle down for a nap.";

    bool asleep;
    bool yawned;
    float settled;
    float lastWarn = -100f;

    protected override void OnBegin()
    {
        asleep = false;
        yawned = false;
        settled = 0f;
    }

    protected override void OnPress(Vector2 local)
    {
        if (asleep && OverPet(local))
            Disturb();
    }

    protected override void OnDrag(Vector2 local, Vector2 delta)
    {
        if (asleep || !OverPet(local))
            return;

        if (Speed > RoughLimit * settleSpeedShare)
        {
            settled = Mathf.Max(0f, settled - delta.magnitude);
            if (Time.unscaledTime - lastWarn > 2f)
            {
                lastWarn = Time.unscaledTime;
                pet.Say("Too fast... now I'm awake again!");
            }
            return;
        }

        settled += delta.magnitude;
        if (!yawned && settled >= settleNeeded * 0.5f)
        {
            yawned = true;
            pet.SleepyYawn();
        }
        if (settled >= settleNeeded)
        {
            asleep = true;
            care.FallAsleep();
        }
    }

    void Disturb()
    {
        if (Time.unscaledTime - lastWarn < 1f)
            return;
        lastWarn = Time.unscaledTime;
        quality -= disturbCost;
        care.needs.Add(Need.Energy, -disturbEnergy);
        pet.Grumble("Mmm... let me sleep...");
    }
}
