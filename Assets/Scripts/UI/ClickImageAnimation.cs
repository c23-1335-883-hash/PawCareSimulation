
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ClickImageAnimation : MonoBehaviour
{
    [Header("Pet Buttons")]
    public Button[] dogs = new Button[10];
    public Button[] cats = new Button[10];

    [Header("Animation Images - With Frame")]
    public Sprite[] dogImages = new Sprite[10];
    public Sprite[] catImages = new Sprite[10];

    [Header("Original Pet Sprites - No Frame")]
    public Sprite[] dogOriginalSprites = new Sprite[10];
    public Sprite[] catOriginalSprites = new Sprite[10];

    [Header("Animation")]
    [Tooltip("Old clip-based animation. Kept for reference; the card now animates from code.")]
    public Animator animator;
    public Image animatedPetImage;

    [Header("Card Feel")]
    [Tooltip("Dark full-screen image behind the card. Tapping it closes the card.")]
    public Image overlay;
    // Pop, close and overlay values live in FeelConfig.
    [Tooltip("Pause after the confetti before the loading screen appears.")]
    public float chooseDelay = 0.6f;
    [Tooltip("Asks for the pet's name after choosing. Optional.")]
    public NamePopup namePopup;

    [Header("Action Buttons")]
    public Button selectNowButton;
    public Button closeButton;

    public string nextSceneName = "home";

    public static Sprite SelectedPetImage;

    private int selectedPetIndex = -1;
    private bool selectedPetIsDog = true;
    private bool closing;
    private bool choosing;
    private CanvasGroup cardGroup;
    private Coroutine cardRoutine;

    void Start()
    {
        cardGroup = animatedPetImage.GetComponent<CanvasGroup>();
        if (cardGroup == null)
            cardGroup = animatedPetImage.gameObject.AddComponent<CanvasGroup>();

        animatedPetImage.gameObject.SetActive(false);
        selectNowButton.gameObject.SetActive(false);
        closeButton.gameObject.SetActive(false);
        if (overlay != null)
            overlay.gameObject.SetActive(false);

        if (animator != null)
            animator.enabled = false;

        // The scene already wires these buttons; only add a listener if it doesn't.
        if (selectNowButton.onClick.GetPersistentEventCount() == 0)
            selectNowButton.onClick.AddListener(SelectNow);
        if (closeButton.onClick.GetPersistentEventCount() == 0)
            closeButton.onClick.AddListener(CloseSelection);

        ButtonJuice juice = selectNowButton.GetComponent<ButtonJuice>();
        if (juice != null)
            juice.idlePulse = true;

        // Dog buttons
        for (int i = 0; i < dogs.Length; i++)
        {
            int index = i;

            if (dogs[index] != null)
            {
                dogs[index].onClick.AddListener(() =>
                {
                    ShowPetAnimation(index, true);
                });
            }
        }

        // Cat buttons
        for (int i = 0; i < cats.Length; i++)
        {
            int index = i;

            if (cats[index] != null)
            {
                cats[index].onClick.AddListener(() =>
                {
                    ShowPetAnimation(index, false);
                });
            }
        }
    }

    void ShowPetAnimation(int index, bool isDog)
    {
        if (choosing || animatedPetImage.gameObject.activeSelf)
            return;

        Sprite animationSprite = isDog
            ? dogImages[index]
            : catImages[index];

        if (animationSprite == null)
        {
            Debug.LogError("Missing animation sprite at index " + index);
            return;
        }

        selectedPetIndex = index;
        selectedPetIsDog = isDog;
        closing = false;

        animatedPetImage.sprite = animationSprite;
        animatedPetImage.gameObject.SetActive(true);
        selectNowButton.gameObject.SetActive(true);
        closeButton.gameObject.SetActive(true);
        if (overlay != null)
            overlay.gameObject.SetActive(true);

        SFXManager.Play(isDog ? Sfx.Bark : Sfx.Meow);
        RunCard(Pop());
    }

    public void SelectNow()
    {
        if (choosing || closing || selectedPetIndex < 0)
            return;

        if (selectedPetIsDog)
        {
            if (selectedPetIndex >= dogOriginalSprites.Length)
                return;

            SelectedPetImage = dogOriginalSprites[selectedPetIndex];
        }
        else
        {
            if (selectedPetIndex >= catOriginalSprites.Length)
                return;

            SelectedPetImage = catOriginalSprites[selectedPetIndex];
        }

        if (SelectedPetImage == null)
        {
            Debug.LogError(
                "Original sprite is missing for index: " + selectedPetIndex);
            return;
        }

        choosing = true;
        RunCard(Choose());
    }

    public void CloseSelection()
    {
        if (choosing || closing || !animatedPetImage.gameObject.activeSelf)
            return;

        closing = true;
        RunCard(Close());
    }

    void RunCard(IEnumerator routine)
    {
        if (cardRoutine != null)
            StopCoroutine(cardRoutine);
        cardRoutine = StartCoroutine(routine);
    }

    IEnumerator Pop()
    {
        Transform card = animatedPetImage.transform;
        float popTime = FeelConfig.Current.cardPopTime;
        for (float t = 0f; t < popTime; t += Time.unscaledDeltaTime)
        {
            float k = t / popTime;
            card.localScale = ReduceMotion.On ? Vector3.one : Vector3.one * Ease.OutBack(k, FeelConfig.Current.cardOvershoot);
            cardGroup.alpha = Mathf.Clamp01(k * 2.5f);
            SetOverlay(Ease.OutQuad(k));
            yield return null;
        }
        card.localScale = Vector3.one;
        cardGroup.alpha = 1f;
        SetOverlay(1f);
        cardRoutine = null;
    }

    IEnumerator Close()
    {
        Transform card = animatedPetImage.transform;
        Vector3 start = card.localScale;
        float closeTime = FeelConfig.Current.cardCloseTime;
        for (float t = 0f; t < closeTime; t += Time.unscaledDeltaTime)
        {
            float k = t / closeTime;
            if (!ReduceMotion.On)
                card.localScale = Vector3.Lerp(start, start * 0.8f, k * k);
            cardGroup.alpha = 1f - k;
            SetOverlay(1f - k);
            yield return null;
        }

        animatedPetImage.sprite = null;
        animatedPetImage.gameObject.SetActive(false);
        selectNowButton.gameObject.SetActive(false);
        closeButton.gameObject.SetActive(false);
        if (overlay != null)
            overlay.gameObject.SetActive(false);

        selectedPetIndex = -1;
        closing = false;
        cardRoutine = null;
    }

    IEnumerator Choose()
    {
        SFXManager.Play(Sfx.Jingle);

        UIBurst fx = UIBurst.Instance;
        if (fx != null && !ReduceMotion.On)
        {
            Vector3 at = selectNowButton.transform.position;
            fx.Burst(at, fx.confetti, 22, UIBurst.Confetti,
                new Color(1f, 0.62f, 0.55f), new Color(1f, 0.84f, 0.45f),
                new Color(0.6f, 0.85f, 0.7f), new Color(0.62f, 0.75f, 1f));
            fx.Burst(at, fx.paw, 6, UIBurst.Confetti, new Color(0.55f, 0.36f, 0.25f));
        }

        // A happy little bounce of the card while the confetti flies.
        Transform card = animatedPetImage.transform;
        const float bounceTime = 0.3f;
        for (float t = 0f; t < bounceTime && !ReduceMotion.On; t += Time.unscaledDeltaTime)
        {
            float k = t / bounceTime;
            card.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.06f);
            yield return null;
        }
        card.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, chooseDelay - bounceTime));
        cardRoutine = null;

        // Next the player names the pet; without a popup, go straight on.
        if (namePopup != null)
            namePopup.Show(!selectedPetIsDog, Adopt);
        else
            Adopt(null);
    }

    // Picking a pet here is adopting a new one: start a fresh save and a baby pet.
    void Adopt(string petName)
    {
        if (!SceneLoader.IsLoading)
        {
            SaveSystem.StartNewGame();
            SaveSystem.Data.petName = petName;
            SaveSystem.Data.isCat = !selectedPetIsDog;
            SaveSystem.Save();
            PetGrowth.StartAsNewPet = true;
        }
        SceneLoader.Load(nextSceneName);
    }

    void SetOverlay(float k)
    {
        if (overlay == null)
            return;
        Color c = overlay.color;
        c.a = FeelConfig.Current.cardOverlayAlpha * k;
        overlay.color = c;
    }
}
