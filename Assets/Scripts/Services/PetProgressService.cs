// Reads and writes the current pet's record and growth progress in the save
// file. PetGrowth keeps only the growth rules; this is the one place where
// growth touches SaveData (Care Points themselves live in CarePoints).
public static class PetProgressService
{
    // True when the save already holds progress for this breed (Continue),
    // false for a newly adopted pet.
    public static bool HasProgressFor(string breedId)
    {
        SaveData save = SaveSystem.Data;
        return save.hasPet && save.petId == breedId;
    }

    public static string SavedPetId => SaveSystem.Data.hasPet ? SaveSystem.Data.petId : null;
    public static int SavedStage => SaveSystem.Data.growthStage;

    // Makes this breed the current pet. A name the player chose is kept;
    // a pet without one is called by its breed. A personality is picked once.
    public static void RegisterPet(PetStageLibrary.Breed breed)
    {
        SaveData save = SaveSystem.Data;
        save.hasPet = true;
        save.petId = breed.id;
        save.isCat = breed.isCat;
        if (string.IsNullOrEmpty(save.petName))
            save.petName = breed.displayName;
        if (!save.hasTrait)
            PetProfile.PickTrait();
        SaveSystem.MarkDirty();
    }

    // Stores the evolution stage the pet is shown at and writes the file
    // straight away, so an evolution is never lost.
    public static void SaveStage(int stage)
    {
        SaveSystem.Data.growthStage = stage;
        SaveSystem.Save();
    }
}
