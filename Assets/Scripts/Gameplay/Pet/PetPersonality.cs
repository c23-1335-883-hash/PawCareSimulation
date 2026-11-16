using UnityEngine;

public enum Trait
{
    Playful,
    Lazy,
    Foodie,
    Clingy,
    Shy
}

// Who the current pet is: its name, species and personality, read from the save.
// Each personality has one favourite care action that gets a bigger reaction
// and a happiness bonus.
public static class PetProfile
{
    public static string Name
    {
        get
        {
            string name = SaveSystem.Data.petName;
            return string.IsNullOrEmpty(name) ? "your pet" : name;
        }
    }

    public static bool HasName => !string.IsNullOrEmpty(SaveSystem.Data.petName);
    public static bool IsCat => SaveSystem.Data.isCat;
    public static Trait Trait => (Trait)Mathf.Clamp(SaveSystem.Data.trait, 0, 4);

    // Called once when a new pet is adopted.
    public static void PickTrait()
    {
        SaveSystem.Data.trait = Random.Range(0, 5);
        SaveSystem.Data.hasTrait = true;
        SaveSystem.MarkDirty();
    }

    public static bool Loves(CareActivity activity)
    {
        switch (Trait)
        {
            case Trait.Playful: return activity == CareActivity.Play;
            case Trait.Foodie: return activity == CareActivity.Feed;
            case Trait.Shy: return activity == CareActivity.Groom;
            default: return false;
        }
    }

    // Lazy pets love naps, clingy pets love cuddles; these aren't CareActivity values.
    public static bool LovesRest => Trait == Trait.Lazy;
    public static bool LovesPetting => Trait == Trait.Clingy;

    public static string TraitName => Trait.ToString();

    public static string TraitBlurb
    {
        get
        {
            switch (Trait)
            {
                case Trait.Playful: return "loves playtime";
                case Trait.Lazy: return "loves cosy naps";
                case Trait.Foodie: return "loves mealtime";
                case Trait.Clingy: return "loves cuddles";
                default: return "loves gentle brushing";
            }
        }
    }

    // Replaces "your pet" in UI text with the pet's name.
    public static string Fill(string text)
    {
        if (!HasName || string.IsNullOrEmpty(text))
            return text;
        return text.Replace("Your pet", Name).Replace("your pet", Name);
    }
}
