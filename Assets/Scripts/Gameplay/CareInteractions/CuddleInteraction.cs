using UnityEngine;

// A proper cuddle: stroke the pet gently for a while. Each pet has a secret
// favourite spot (ears, back or tummy) that it loves most; finding it makes
// the cuddle perfect. Rough strokes make it flinch.
public class CuddleInteraction : CareInteraction
{
    [Tooltip("Total stroke distance (canvas units) for a full cuddle.")]
    public float strokeNeeded = 1400f;
    public float spotRadius = 90f;
    [Tooltip("Best rating possible without finding the favourite spot.")]
    public float qualityWithoutSpot = 0.8f;

    public override string Prompt => "Stroke your pet gently. Can you find its favourite spot?";

    static readonly Vector2[] Spots = { new Vector2(0.5f, 0.85f), new Vector2(0.35f, 0.55f), new Vector2(0.6f, 0.3f) };
    static readonly string[] SpotLines = { "Ooh, behind the ears!", "Ahh, right on my back!", "Hehe, my tummy!" };

    int favourite;
    float stroked;
    float sinceHeart;
    bool found;

    // Which spot (0 = ears, 1 = back, 2 = tummy) the pet loves most this time.
    protected virtual int PickFavourite()
    {
        return Random.Range(0, Spots.Length);
    }

    protected override void OnBegin()
    {
        favourite = PickFavourite();
        stroked = 0f;
        sinceHeart = 0f;
        found = false;
    }

    protected override void OnDrag(Vector2 local, Vector2 delta)
    {
        if (!OverPet(local))
            return;
        if (CheckRough("Easy there, gently!"))
            return;

        Vector2 spot = Spots[favourite];
        bool onSpot = (PetLocal(spot.x, spot.y) - local).magnitude < spotRadius;
        float distance = delta.magnitude * (onSpot ? 2f : 1f);
        stroked += distance;
        sinceHeart += distance;
        if (onSpot && !found)
        {
            found = true;
            pet.Say(SpotLines[favourite]);
            care.ShowPrompt("You found " + PetProfile.Name + "'s favourite spot!");
        }
        if (sinceHeart >= 140f)
        {
            sinceHeart = 0f;
            pet.Nuzzle(ToWorld(local), onSpot);
        }

        if (stroked < strokeNeeded)
            return;
        if (!found)
            quality = Mathf.Min(quality, qualityWithoutSpot);
        Finish();
    }
}
