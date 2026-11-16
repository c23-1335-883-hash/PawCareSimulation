using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Brushing: tangles show up in the fur and each needs a few smooth brush
// strokes to come out. Brushing too fast pulls the fur and the pet flinches;
// shy pets are the most sensitive.
public class GroomInteraction : CareInteraction
{
    public int tangles = 4;
    [Tooltip("Separate strokes needed over a tangle to brush it out.")]
    public int strokesPerTangle = 2;
    public float brushRadius = 60f;
    public float tangleSize = 40f;
    public Color tangleColor = new Color(0.45f, 0.3f, 0.18f, 0.85f);

    public override CareActivity? Activity => CareActivity.Groom;
    public override string Prompt => "Brush out the tangles with smooth, gentle strokes.";

    readonly Dictionary<Image, int> strokes = new Dictionary<Image, int>();
    readonly HashSet<Image> touchedThisStroke = new HashSet<Image>();
    float sinceSparkle;

    protected override void OnBegin()
    {
        strokes.Clear();
        touchedThisStroke.Clear();
        sinceSparkle = 0f;
        for (int i = 0; i < tangles; i++)
        {
            // Spread them out across the body, away from the face.
            float x = Mathf.Lerp(0.22f, 0.78f, (i + Random.Range(0.2f, 0.8f)) / tangles);
            float y = Random.Range(0.25f, 0.6f);
            strokes[AddMarker(PetLocal(x, y), tangleColor, tangleSize)] = 0;
        }
    }

    protected override void OnEnd()
    {
        strokes.Clear();
        touchedThisStroke.Clear();
    }

    protected override void OnPress(Vector2 local)
    {
        touchedThisStroke.Clear();
    }

    protected override void OnDrag(Vector2 local, Vector2 delta)
    {
        if (!OverPet(local))
            return;
        if (CheckRough("Ouch! That pulls my fur!"))
            return;

        sinceSparkle += delta.magnitude;
        if (sinceSparkle >= 80f)
        {
            sinceSparkle = 0f;
            pet.ActivityStep(ToWorld(local));
        }

        for (int i = Markers.Count - 1; i >= 0; i--)
        {
            Image tangle = Markers[i];
            if (touchedThisStroke.Contains(tangle) || (MarkerLocal(tangle) - local).magnitude > brushRadius)
                continue;
            touchedThisStroke.Add(tangle);
            int done = ++strokes[tangle];
            if (done < strokesPerTangle)
            {
                // Loosening: the tangle shrinks and fades a little each stroke.
                float k = 1f - (float)done / strokesPerTangle;
                tangle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, k);
                Color c = tangle.color;
                c.a = tangleColor.a * Mathf.Lerp(0.4f, 1f, k);
                tangle.color = c;
                continue;
            }
            pet.FurPuff(tangle.rectTransform.position);
            SFXManager.Play(Sfx.Pop, 0.5f);
            strokes.Remove(tangle);
            RemoveMarker(tangle);
            if (Markers.Count > 0 && Random.value < 0.5f)
                pet.Say(PetProfile.Trait == Trait.Shy ? "That feels nice..." : "Ahh, much better!");
        }
        if (Markers.Count == 0)
            Finish();
    }

    protected override void OnRelease(Vector2 local)
    {
        touchedThisStroke.Clear();
    }
}
