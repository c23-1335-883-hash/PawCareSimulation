using UnityEngine;

public class SceneButton : MonoBehaviour
{
    public void GoSelection()
    {
        SceneLoader.Load("selection");
    }

    public void GoTohome()
    {
        SceneLoader.Load("home");
    }

    // Hooked to the Play button at home: opens the mini-games menu.
    public void GoToMiniGames()
    {
        SceneLoader.Load("minigames");
    }

    public void GoToMainMenu()
    {
        SceneLoader.Load("start");
    }
}
