using UnityEngine;
using UnityEngine.SceneManagement;

// Background music for the whole game (sound effects live in SFXManager).
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    public AudioClip menuMusic;
    public AudioClip gameMusic;

    AudioSource audioSource;

    public float MusicVolume { get; private set; }

    void Awake()
    {
        // The start scene creates a MusicManager each time it loads;
        // keep the first one so the music doesn't restart or double up.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        MusicVolume = SaveSystem.Data.musicVolume;
        audioSource.volume = MusicVolume;

        SceneManager.sceneLoaded += ChangeMusic;
    }

    void Start()
    {
        if (Instance == this)
            ChangeMusic(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= ChangeMusic;
        Instance = null;
    }

    void ChangeMusic(Scene scene, LoadSceneMode mode)
    {
        AudioClip clip = scene.name == "home" ? gameMusic : menuMusic;
        if (clip == null)
            return;

        // Only restart when the track actually changes.
        if (audioSource.clip == clip && audioSource.isPlaying)
            return;

        audioSource.clip = clip;
        audioSource.Play();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        audioSource.volume = MusicVolume;
        SaveSystem.Data.musicVolume = MusicVolume;
        SaveSystem.MarkDirty();
    }

    // SFXManager reads this from the save data each time it plays a sound.
    public void SetSfxVolume(float value)
    {
        SaveSystem.Data.sfxVolume = Mathf.Clamp01(value);
        SaveSystem.MarkDirty();
    }

    // This object lives for the whole session, so it handles the
    // "app sent to background / closed" checkpoints for the save file.
    void OnApplicationPause(bool paused)
    {
        if (paused && Instance == this)
            SaveSystem.SaveIfDirty();
    }

    void OnApplicationQuit()
    {
        if (Instance == this)
            SaveSystem.SaveIfDirty();
    }
}
