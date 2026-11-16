using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Name your puppy!" popup shown after choosing a pet. The text field starts
// with a cute suggestion; the dice button picks another one, and the paw
// button confirms. An empty name falls back to the suggestion.
public class NamePopup : MonoBehaviour
{
    public CanvasGroup group;
    public RectTransform card;
    public TMP_Text heading;
    public TMP_InputField input;
    public Button confirmButton;
    public Button diceButton;
    public int maxLength = 12;

    public string[] dogNames = { "Biscuit", "Mochi", "Peanut", "Buddy", "Coco", "Waffles", "Bean", "Pudding", "Cookie", "Teddy" };
    public string[] catNames = { "Luna", "Mochi", "Whiskers", "Nala", "Tofu", "Miso", "Pumpkin", "Bean", "Kiki", "Sushi" };

    Action<string> onDone;
    bool isCat;
    bool closing;

    void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(Confirm);
        if (diceButton != null)
            diceButton.onClick.AddListener(Randomize);
        if (input != null)
        {
            input.characterLimit = maxLength;
            input.richText = false;
            input.onSubmit.AddListener(_ => Confirm());
        }
        // The popup starts hidden in the scene; Show() turns it on.
    }

    public void Show(bool cat, Action<string> done)
    {
        isCat = cat;
        onDone = done;
        closing = false;
        if (heading != null)
            heading.text = cat ? "Name your kitten!" : "Name your puppy!";
        gameObject.SetActive(true);
        Randomize();
        StartCoroutine(Pop());
    }

    public void Randomize()
    {
        string[] names = isCat ? catNames : dogNames;
        string current = input.text;
        string pick = current;
        for (int i = 0; i < 5 && pick == current; i++)
            pick = names[UnityEngine.Random.Range(0, names.Length)];
        input.text = pick;
    }

    void Confirm()
    {
        if (closing)
            return;
        closing = true;
        string name = Clean(input.text);
        if (name.Length == 0)
            name = (isCat ? catNames : dogNames)[0];
        SFXManager.Play(Sfx.HappyChirp);
        onDone?.Invoke(name);
    }

    // Trims spaces, removes rich-text tags like <b> and caps the length.
    string Clean(string raw)
    {
        if (raw == null)
            return "";
        var sb = new System.Text.StringBuilder();
        bool inTag = false;
        foreach (char c in raw)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag) sb.Append(c);
        }
        string s = sb.ToString().Trim();
        if (s.Length > maxLength)
            s = s.Substring(0, maxLength).Trim();
        return s;
    }

    IEnumerator Pop()
    {
        const float time = 0.3f;
        group.blocksRaycasts = true;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            group.alpha = Mathf.Clamp01(k * 2f);
            card.localScale = ReduceMotion.On ? Vector3.one : Vector3.one * Ease.OutBack(k);
            yield return null;
        }
        group.alpha = 1f;
        card.localScale = Vector3.one;
    }
}
