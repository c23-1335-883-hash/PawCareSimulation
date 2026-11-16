// Upset tummy: a slow, gentle tummy rub. Works like a cuddle (CuddleInteraction)
// with the tummy as the spot that matters.
public class TummyRubInteraction : CuddleInteraction
{
    public override string Prompt => "Gently rub your pet's tummy with slow strokes.";

    protected override int PickFavourite()
    {
        return 2; // tummy
    }
}
