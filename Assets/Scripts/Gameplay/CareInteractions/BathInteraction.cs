using UnityEngine;
using UnityEngine.UI;

// Bath time in two steps: scrub foam into the fur all over the pet, then
// rinse every bit of foam away. Scrubbing too hard makes the pet flinch.
public class BathInteraction : CareInteraction
{
    [Tooltip("Foam patches to work in before rinsing.")]
    public int foamNeeded = 8;
    [Tooltip("Scrub distance (canvas units) per foam patch.")]
    public float scrubPerFoam = 90f;
    [Tooltip("New foam only goes where there isn't any yet, so the whole pet gets washed.")]
    public float foamSpacing = 55f;
    public float rinseRadius = 70f;
    public Color foamColor = new Color(1f, 1f, 1f, 0.9f);
    [Tooltip("Shown in place of the sponge while rinsing (optional).")]
    public Sprite rinseTool;

    public override CareActivity? Activity => CareActivity.Clean;
    public override string Prompt => "Scrub your pet all over with the sponge to make foam.";

    bool rinsing;
    float scrubbed;
    float sinceBubbles;
    Sprite sponge;

    protected override void OnBegin()
    {
        rinsing = false;
        scrubbed = 0f;
        sinceBubbles = 0f;
        sponge = Tool != null ? Tool.sprite : null;
    }

    protected override void OnEnd()
    {
        if (Tool != null)
            Tool.sprite = sponge;
    }

    protected override void OnDrag(Vector2 local, Vector2 delta)
    {
        if (!OverPet(local))
            return;
        if (rinsing)
            Rinse(local);
        else
            Scrub(local, delta.magnitude);
    }

    void Scrub(Vector2 local, float distance)
    {
        if (CheckRough("Ow! Gently, please!"))
            return;

        scrubbed += distance;
        sinceBubbles += distance;
        if (sinceBubbles >= 45f)
        {
            sinceBubbles = 0f;
            pet.ActivityStep(ToWorld(local));
        }
        if (scrubbed < scrubPerFoam || HasFoamNear(local, foamSpacing))
            return;

        scrubbed = 0f;
        Image foam = AddMarker(local, foamColor, Random.Range(46f, 64f));
        foam.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        if (Markers.Count >= foamNeeded)
        {
            rinsing = true;
            if (Tool != null && rinseTool != null)
                Tool.sprite = rinseTool;
            pet.Say("So bubbly!");
            care.ShowPrompt("Now rinse all the foam off.");
        }
    }

    void Rinse(Vector2 local)
    {
        for (int i = Markers.Count - 1; i >= 0; i--)
        {
            Image foam = Markers[i];
            if ((MarkerLocal(foam) - local).magnitude > rinseRadius)
                continue;
            pet.Splash(foam.rectTransform.position);
            SFXManager.Play(Sfx.Pop, 0.5f);
            RemoveMarker(foam);
        }
        if (Markers.Count == 0)
            Finish();
    }

    bool HasFoamNear(Vector2 local, float distance)
    {
        foreach (Image foam in Markers)
        {
            if ((MarkerLocal(foam) - local).magnitude < distance)
                return true;
        }
        return false;
    }
}
