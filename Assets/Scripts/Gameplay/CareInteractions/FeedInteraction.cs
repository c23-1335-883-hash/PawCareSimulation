using UnityEngine;

// Feeding by hand: pick up the bowl and hold it under the pet's mouth, and
// it eats bite by bite. Holding the bowl still is polite; waving it around
// makes the pet back off. Foodie pets eat faster.
public class FeedInteraction : CareInteraction
{
    public int bites = 5;
    public float biteEvery = 0.6f;
    [Tooltip("How close the bowl must be to the pet's mouth (canvas units).")]
    public float reach = 150f;

    public override CareActivity? Activity => CareActivity.Feed;
    public override string Prompt => "Pick up the bowl and hold it near your pet's mouth.";
    protected virtual string SniffLine => PetProfile.Trait == Trait.Foodie ? "Ooh, food! Food!" : "*sniff sniff*";
    protected virtual string KeepHolding => "Keep holding the bowl until your pet has finished";

    int eaten;
    float biteTimer;
    bool sniffed;

    protected override void OnBegin()
    {
        eaten = 0;
        biteTimer = 0f;
        sniffed = false;
    }

    void Update()
    {
        if (!Running || !Holding)
            return;

        Vector2 mouth = PetLocal(0.5f, 0.55f);
        if ((Pointer + toolOffset - mouth).magnitude > reach)
        {
            biteTimer = 0f;
            return;
        }
        if (!sniffed)
        {
            sniffed = true;
            pet.Say(SniffLine);
        }
        if (CheckRough("Hold it still, please!"))
        {
            biteTimer = 0f;
            return;
        }

        biteTimer += Time.deltaTime;
        float every = PetProfile.Trait == Trait.Foodie ? biteEvery * 0.6f : biteEvery;
        if (biteTimer < every)
            return;
        biteTimer = 0f;
        eaten++;
        pet.ActivityStep();
        if (eaten >= bites)
            Finish();
    }

    protected override void OnRelease(Vector2 local)
    {
        if (Running && eaten > 0)
            care.ShowPrompt(KeepHolding + " (" + eaten + "/" + bites + ").");
    }
}
