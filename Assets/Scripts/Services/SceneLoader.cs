using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shows the loading screen between scenes. Call SceneLoader.Load("sceneName")
// from any script. The loader creates itself from Resources/LoadingScreen the
// first time it's needed and then stays alive across scenes (one instance only).
//
// The bar shows real scene-loading progress. Unity reports loading as 0-90%
// and the last step is activating the scene, so the label switches to
// "Almost ready" for that step instead of claiming 100% too early.
public class SceneLoader : MonoBehaviour
{
    [Serializable]
    public class SceneMessage
    {
        public string scene;
        public string message;
    }

    [Header("UI")]
    public Canvas canvas;
    public CanvasGroup group;
    public Image progressFill;
    public RectTransform progressTrack;
    public RectTransform progressPaw;
    public TMP_Text messageText;
    public TMP_Text percentText;

    [Header("Messages")]
    public SceneMessage[] messages =
    {
        new SceneMessage { scene = "start", message = "Heading back to the main menu" },
        new SceneMessage { scene = "selection", message = "Getting your furry friends ready" },
        new SceneMessage { scene = "home", message = "Preparing your pet's home" },
    };
    public string defaultMessage = "Loading your next adventure";

    [Header("Timing")]
    public float fadeTime = 0.25f;
    [Tooltip("Shortest time the screen stays up, so fast loads still look smooth.")]
    public float minimumShowTime = 0.7f;
    [Tooltip("How fast the bar can fill (fraction per second).")]
    public float barSpeed = 2.5f;

    const string PrefabPath = "LoadingScreen";

    static SceneLoader instance;

    public static bool IsLoading => instance != null && instance.loading;

    bool loading;
    string loadingScene;
    string message;
    float shownProgress;

    public static bool Load(string sceneName)
    {
        if (instance == null)
        {
            SceneLoader prefab = Resources.Load<SceneLoader>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("SceneLoader: Resources/" + PrefabPath + " is missing. Loading '" + sceneName + "' without a loading screen.");
                SceneManager.LoadScene(sceneName);
                return true;
            }
            Instantiate(prefab);
        }
        return instance.Begin(sceneName);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        Hide();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    bool Begin(string sceneName)
    {
        if (loading)
        {
            // Repeated taps on the same button are expected; only warn about a different scene.
            if (sceneName != loadingScene)
                Debug.LogWarning("SceneLoader: already loading '" + loadingScene + "', ignoring request for '" + sceneName + "'.");
            return false;
        }
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("SceneLoader: scene '" + sceneName + "' doesn't exist or isn't in the Build Profile scene list.");
            return false;
        }

        StartCoroutine(LoadRoutine(sceneName));
        return true;
    }

    IEnumerator LoadRoutine(string sceneName)
    {
        loading = true;
        loadingScene = sceneName;
        message = MessageFor(sceneName);
        SaveSystem.SaveIfDirty(); // checkpoint before leaving a scene
        shownProgress = 0f;
        SetProgress(0f);
        percentText.text = "0%";

        // Show the screen and block clicks on the old scene.
        canvas.enabled = true;
        group.blocksRaycasts = true;
        yield return Fade(0f, 1f);
        float shownAt = Time.unscaledTime;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError("SceneLoader: Unity could not start loading '" + sceneName + "'.");
            yield return Fade(1f, 0f);
            Hide();
            yield break;
        }
        op.allowSceneActivation = false;

        // Loading phase: Unity's progress goes 0 -> 0.9 while the scene loads.
        while (true)
        {
            float target = Mathf.Clamp01(op.progress / 0.9f);
            shownProgress = Mathf.MoveTowards(shownProgress, target, barSpeed * Time.unscaledDeltaTime);
            SetProgress(shownProgress);
            percentText.text = Mathf.RoundToInt(shownProgress * 100f) + "%";

            bool loaded = op.progress >= 0.9f && shownProgress >= 1f;
            if (loaded && Time.unscaledTime - shownAt >= minimumShowTime)
                break;
            yield return null;
        }

        // Activation phase: the new scene starts up behind the loading screen.
        message = "Almost ready";
        percentText.text = "";
        op.allowSceneActivation = true;
        while (!op.isDone)
            yield return null;
        yield return null; // let the new scene run its Start methods

        yield return Fade(1f, 0f);
        Hide();
    }

    void Update()
    {
        if (!loading)
            return;

        // Animated dots and a gently bouncing paw riding the end of the bar.
        int dots = (int)(Time.unscaledTime * 2.5f) % 4;
        messageText.text = message + new string('.', dots);

        if (progressPaw != null && progressTrack != null)
        {
            float x = progressTrack.rect.width * shownProgress;
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 14f;
            progressPaw.anchoredPosition = new Vector2(x, 28f + bounce);
        }
    }

    IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / fadeTime);
            yield return null;
        }
        group.alpha = to;
    }

    void Hide()
    {
        loading = false;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        canvas.enabled = false;
    }

    void SetProgress(float value)
    {
        progressFill.fillAmount = value;
    }

    string MessageFor(string sceneName)
    {
        foreach (SceneMessage m in messages)
        {
            if (m.scene == sceneName)
                return m.message;
        }
        return defaultMessage;
    }
}
