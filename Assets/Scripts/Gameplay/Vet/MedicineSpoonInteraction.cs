// Cold medicine: hold the spoon of syrup at the pet's mouth and keep it steady
// for a few sips. It works just like hand-feeding (FeedInteraction).
public class MedicineSpoonInteraction : FeedInteraction
{
    public override CareActivity? Activity => null;
    public override string Prompt => "Hold the spoon of medicine at your pet's mouth and keep it steady.";
    protected override string SniffLine => "Hmm... medicine? Okay, just a little.";
    protected override string KeepHolding => "Keep holding the spoon until all the medicine is gone";
}
