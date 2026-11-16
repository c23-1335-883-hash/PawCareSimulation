using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A once-over: tap each glowing spot (ears, eyes, teeth, fur) to check it.
// SpotChecked tells the vet clinic which spot it was, so the vet can say what
// was found there.
public class InspectInteraction : CareInteraction
{
    public float tapRadius = 80f;
    public float spotSize = 60f;
    public Color spotColor = new Color(1f, 0.93f, 0.55f, 0.75f);

    public override string Prompt => "Tap each glowing spot to check your pet's ears, eyes, teeth and fur.";

    // Fired with the spot index: 0 = ears, 1 = eyes, 2 = teeth, 3 = fur.
    public event Action<int> SpotChecked;

    static readonly Vector2[] Spots = { new Vector2(0.3f, 0.92f), new Vector2(0.45f, 0.8f), new Vector2(0.5f, 0.66f), new Vector2(0.62f, 0.38f) };

    readonly Dictionary<Image, int> spots = new Dictionary<Image, int>();

    protected override void OnBegin()
    {
        spots.Clear();
        for (int i = 0; i < Spots.Length; i++)
            spots[AddMarker(PetLocal(Spots[i].x, Spots[i].y), spotColor, spotSize)] = i;
    }

    protected override void OnEnd()
    {
        spots.Clear();
    }

    void Update()
    {
        if (!Running)
            return;
        float s = 1f + Mathf.Sin(Time.time * 5f) * 0.15f;
        foreach (Image spot in Markers)
            spot.rectTransform.localScale = Vector3.one * s;
    }

    protected override void OnPress(Vector2 local)
    {
        Image nearest = null;
        float best = tapRadius;
        foreach (Image spot in Markers)
        {
            float d = (MarkerLocal(spot) - local).magnitude;
            if (d < best)
            {
                best = d;
                nearest = spot;
            }
        }
        if (nearest == null)
            return;

        int index = spots[nearest];
        pet.Nuzzle(nearest.rectTransform.position, false);
        SFXManager.Play(Sfx.Pop, 0.5f);
        spots.Remove(nearest);
        RemoveMarker(nearest);
        SpotChecked?.Invoke(index);
        if (Markers.Count == 0)
            Finish();
    }
}
