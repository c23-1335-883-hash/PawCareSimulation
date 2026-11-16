using System;
using UnityEngine;

// Lists every breed and the sprite for each of its growth stages.
// Each breed picture in Sprites/Pets shows four ages left to right
// (baby, young, adult, senior), which map to stages 0-3 here.
[CreateAssetMenu(menuName = "PAWcare/Pet Stage Library")]
public class PetStageLibrary : ScriptableObject
{
    [Serializable]
    public class Breed
    {
        public string id;
        public string displayName;
        public bool isCat;
        public Sprite[] stages = new Sprite[4];
    }

    public string[] dogStageNames = { "Puppy", "Young", "Adult", "Senior" };
    public string[] catStageNames = { "Kitten", "Young", "Adult", "Senior" };
    public Breed[] breeds;

    // Finds the breed that owns this sprite (any stage).
    public Breed Find(Sprite sprite)
    {
        if (sprite == null)
            return null;
        foreach (Breed b in breeds)
        {
            foreach (Sprite s in b.stages)
            {
                if (s == sprite)
                    return b;
            }
        }
        foreach (Breed b in breeds)
        {
            if (b.stages.Length > 0 && b.stages[0] != null && b.stages[0].texture == sprite.texture)
                return b;
        }
        return null;
    }

    public Breed FindById(string id)
    {
        foreach (Breed b in breeds)
        {
            if (b.id == id)
                return b;
        }
        return null;
    }

    public string StageName(Breed breed, int stage)
    {
        string[] names = breed.isCat ? catStageNames : dogStageNames;
        return names[Mathf.Clamp(stage, 0, names.Length - 1)];
    }

    // "Beagle Puppy", "Young Beagle", "Adult Beagle", ...
    public string Title(Breed breed, int stage)
    {
        string stageName = StageName(breed, stage);
        return stage == 0 ? breed.displayName + " " + stageName : stageName + " " + breed.displayName;
    }
}
