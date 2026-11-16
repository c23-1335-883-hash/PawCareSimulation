using UnityEngine;
using UnityEngine.UI;

// A vaccine in two steps: first calm the pet with slow strokes, then give the
// shot by tapping the shoulder when the pulsing ring is at its smallest.
// Shy pets need more calming. A badly timed tap makes the pet flinch.
public class VaccineInteraction : CareInteraction
{
    [Tooltip("Slow stroke distance (canvas units) to calm the pet.")]
    public float calmNeeded = 600f;
    [Tooltip("Calming needs slow strokes (share of the rough speed).")]
    public float calmSpeedShare = 0.5f;
    [Tooltip("Seconds for the ring to shrink and grow back.")]
    public float ringPeriod = 1.4f;
    [Tooltip("How close to the smallest ring a tap must be (share of the period).")]
    public float tapWindow = 0.15f;
    public float missCost = 0.2f;
    [Tooltip("After this many tries the shot is given anyway.")]
    public int maxTries = 3;
    public Color ringColor = new Color(0.45f, 0.75f, 1f, 0.8f);

    public override string Prompt => "First, stroke your pet slowly to calm it down.";

    Vector2 Shoulder => PetLocal(0.65f, 0.6f);

    float calmed;
    float sinceHeart;
    bool calm;
    int tries;
    float ringTime;
    float lastWarn = -100f;
    Image ring;

    protected override void OnBegin()
    {
        calmed = 0f;
        sinceHeart = 0f;
        calm = false;
        tries = 0;
        ring = null;
    }

    float Needed => PetProfile.Trait == Trait.Shy ? calmNeeded * 1.5f : calmNeeded;

    protected override void OnDrag(Vector2 local, Vector2 delta)
    {
        if (calm || !OverPet(local))
            return;
        if (Speed > RoughLimit * calmSpeedShare)
        {
            calmed = Mathf.Max(0f, calmed - delta.magnitude * 0.5f);
            if (Time.unscaledTime - lastWarn > 2f)
            {
                lastWarn = Time.unscaledTime;
                pet.Say("Eep... I'm nervous...");
            }
            return;
        }

        calmed += delta.magnitude;
        sinceHeart += delta.magnitude;
        if (sinceHeart >= 160f)
        {
            sinceHeart = 0f;
            pet.Nuzzle(ToWorld(local), false);
        }
        if (calmed < Needed)
            return;

        calm = true;
        ringTime = 0f;
        ring = AddMarker(Shoulder, ringColor, 120f);
        pet.Say("Okay... I'm ready.");
        care.ShowPrompt("Now tap the shoulder when the blue circle is at its smallest!");
    }

    void Update()
    {
        if (!Running || ring == null)
            return;
        ringTime += Time.deltaTime;
        ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.4f, (Mathf.Cos(Phase * Mathf.PI * 2f) + 1f) * 0.5f);
    }

    // 0-1 through the ring's cycle; smallest at 0.5.
    float Phase => ringTime % ringPeriod / ringPeriod;

    protected override void OnPress(Vector2 local)
    {
        if (!calm || (local - Shoulder).magnitude > 140f)
            return;
        tries++;
        bool good = Mathf.Abs(Phase - 0.5f) <= tapWindow;
        if (!good)
            quality -= missCost;
        if (!good && tries < maxTries)
        {
            pet.Flinch("Ouch! Not yet!");
            return;
        }

        SFXManager.Play(Sfx.Pop, 0.7f);
        UIBurst fx = ReduceMotion.On ? null : UIBurst.Instance;
        if (fx != null && fx.dot != null)
            fx.Burst(ToWorld(Shoulder), fx.dot, 10, PetController.Sparkle, pet.sparkleColor, Color.white);
        pet.Say(good ? "Oh! Was that it? That was quick!" : "Ow... but I was brave!");
        Finish();
    }
}
