
using UnityEngine;
using UnityEngine.UI;

public class SelectedPetDisplay : MonoBehaviour
{
    public Image petImage;

    void Start()
    {
        if (ClickImageAnimation.SelectedPetImage != null)
        {
            petImage.sprite = ClickImageAnimation.SelectedPetImage;
            petImage.gameObject.SetActive(true);
        }
        else if (!SaveSystem.Data.hasPet)
        {
            // Home opened without choosing a pet (e.g. pressing Play in the home scene).
            // A warning, not an error, so the editor's Error Pause doesn't freeze the game.
            Debug.LogWarning("No pet sprite was saved. Start from the main menu to choose a pet.");
        }
        // Otherwise PetGrowth shows the saved pet (Continue) - nothing to do here.
    }
}
