using TMPro;
using UnityEngine;

// Shows and hides the home sidebar. The Sidebar.controller animator slides it
// in or out whenever its "Open" bool changes; this just flips that bool.
public class SidebarToggle : MonoBehaviour
{
    public Animator sidebar;
    public string openParameter = "Open";
    public bool startOpen = true;

    [Header("Label")]
    public TMP_Text label;
    public string openLabel = "<<";
    public string closedLabel = ">>";

    [Header("Animator States (for Reduce Motion)")]
    public string openState = "sidebard";
    public string closedState = "closeSidebar";

    bool open;

    void Start()
    {
        open = startOpen;
        Apply();
    }

    // Hooked to the toggle button's onClick.
    public void Toggle()
    {
        open = !open;
        Apply();
    }

    void Apply()
    {
        if (label != null)
            label.text = open ? openLabel : closedLabel;
        if (sidebar == null)
            return;

        sidebar.SetBool(openParameter, open);
        // With Reduce Motion, skip the slide and jump straight to the end.
        if (ReduceMotion.On)
            sidebar.Play(open ? openState : closedState, 0, 1f);
    }
}
