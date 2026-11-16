using UnityEngine;

// Fleas: tiny hopping fleas in the fur; comb over each one gently to get it out.
// Works like brushing out tangles (GroomInteraction), but the fleas jump around.
public class FleaCombInteraction : GroomInteraction
{
    public float hopEvery = 0.6f;
    public float hopDistance = 26f;

    public override CareActivity? Activity => null;
    public override string Prompt => "Gently comb out every flea you can see.";

    float hopTimer;

    void Update()
    {
        if (!Running)
            return;
        hopTimer += Time.deltaTime;
        if (hopTimer < hopEvery)
            return;
        hopTimer = 0f;
        // A flea or two hops to a nearby spot (staying on the pet).
        foreach (var flea in Markers)
        {
            if (Random.value > 0.4f)
                continue;
            Vector2 to = MarkerLocal(flea) + Random.insideUnitCircle * hopDistance;
            if (OverPet(to, -20f))
                flea.rectTransform.position = ToWorld(to);
        }
    }
}
