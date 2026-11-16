using UnityEngine;

public enum Sfx
{
    Pop,
    Hover,
    Bark,
    Meow,
    Munch,
    Bubbles,
    HappyChirp,
    Jingle,
    Rumble,
    Coin
}

// Plays short sound effects with a little random pitch so repeats don't sound
// robotic. It creates itself from Resources/SFXManager the first time a sound
// is played and stays alive across scenes. Assign clips on that prefab; any
// empty slot is simply silent (Pop falls back to a soft generated click).
public class SFXManager : MonoBehaviour
{
    [Header("Clips (all optional)")]
    public AudioClip pop;
    public AudioClip hover;
    public AudioClip bark;
    public AudioClip meow;
    public AudioClip munch;
    public AudioClip bubbles;
    public AudioClip happyChirp;
    public AudioClip jingle;
    public AudioClip tummyRumble;
    public AudioClip coin;


    const string PrefabPath = "SFXManager";
    static SFXManager instance;
    AudioSource source;
    AudioClip fallbackPop;

    public static void Play(Sfx sound, float volume = 1f)
    {
        if (instance == null)
            Create();
        if (instance != null)
            instance.PlayInternal(sound, volume);
    }

    static void Create()
    {
        SFXManager prefab = Resources.Load<SFXManager>(PrefabPath);
        if (prefab != null)
            Instantiate(prefab);
        else
            new GameObject("SFXManager").AddComponent<SFXManager>();
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
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        fallbackPop = CreateSoftClick();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void PlayInternal(Sfx sound, float volume)
    {
        AudioClip clip = ClipFor(sound);
        if (clip == null)
            return;
        if (sound == Sfx.Hover)
            volume *= FeelConfig.Current.hoverVolume;
        source.pitch = Random.Range(FeelConfig.Current.pitchRange.x, FeelConfig.Current.pitchRange.y);
        source.PlayOneShot(clip, volume * SaveSystem.Data.sfxVolume);
    }

    AudioClip ClipFor(Sfx sound)
    {
        switch (sound)
        {
            case Sfx.Pop: return pop != null ? pop : fallbackPop;
            case Sfx.Hover: return hover;
            case Sfx.Bark: return bark;
            case Sfx.Meow: return meow;
            case Sfx.Munch: return munch;
            case Sfx.Bubbles: return bubbles;
            case Sfx.HappyChirp: return happyChirp;
            case Sfx.Jingle: return jingle;
            case Sfx.Coin: return coin != null ? coin : fallbackPop;
            default: return tummyRumble;
        }
    }

    // A short, soft "pop" generated in code, used until a pop clip is assigned.
    static AudioClip CreateSoftClick()
    {
        const int sampleRate = 44100;
        int length = sampleRate / 12;
        float[] data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(900f, 500f, (float)i / length);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 45f) * 0.35f;
        }
        AudioClip clip = AudioClip.Create("SoftClick", length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

// Shared "Reduce Motion" setting: when on, bounces, wiggles and shakes are skipped.
public static class ReduceMotion
{
    public static bool On => SaveSystem.Data.reduceMotion;
}
