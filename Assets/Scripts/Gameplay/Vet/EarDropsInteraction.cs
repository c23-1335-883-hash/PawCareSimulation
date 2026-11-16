using UnityEngine;
using UnityEngine.UI;

// Ear drops: hold the dropper over the sore ear and keep it steady until each
// drop falls in. Jabbing it around makes the pet pull away.
public class EarDropsInteraction : CareInteraction
{
    public int drops = 3;
    [Tooltip("Seconds to hold steady over the ear for each drop.")]
    public float holdTime = 0.8f;
    public float earRadius = 80f;
    [Tooltip("Share of the rough speed that counts as jabbing.")]
    public float stillShare = 0.4f;
    public Color soreColor = new Color(1f, 0.45f, 0.45f, 0.55f);

    public override string Prompt => "Hold the dropper over the sore ear and keep it steady.";

    Vector2 Ear => PetLocal(0.3f, 0.92f);

    int given;
    float timer;
    Image sore;

    protected override void OnBegin()
    {
        given = 0;
        timer = 0f;
        sore = AddMarker(Ear, soreColor, 64f);
    }

    void Update()
    {
        if (!Running)
            return;
        if (sore != null)
            sore.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 4f) * 0.12f);
        if (!Holding || (Pointer + toolOffset - Ear).magnitude > earRadius)
        {
            timer = 0f;
            return;
        }
        if (Shaky(stillShare, "Ow! Careful with my ear!"))
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;
        if (timer < holdTime)
            return;
        timer = 0f;
        given++;
        pet.ActivityStep(ToWorld(Ear));
        pet.Splash(ToWorld(Ear));
        SFXManager.Play(Sfx.Pop, 0.5f);
        if (sore != null)
        {
            Color c = sore.color;
            c.a = soreColor.a * (1f - (float)given / drops);
            sore.color = c;
        }
        pet.Say(given < drops ? "Brr, that's cold..." : "Ahh, that's better.");
        if (given >= drops)
            Finish();
    }
}
