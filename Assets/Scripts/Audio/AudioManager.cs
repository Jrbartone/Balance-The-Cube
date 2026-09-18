using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Audio;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Global Routing Defaults")]
    [SerializeField] private AudioMixerGroup defaultSFXGroup;
    [SerializeField] private AudioMixerGroup defaultMusicGroup;

    private AudioSource activeMusicSource;
    private AudioSource inactiveMusicSource;
    private ObjectPool<AudioSource> sfxPool;
    private Coroutine crossfadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeMusicSources();
        InitializeSFXPool();
    }

    private void InitializeMusicSources()
    {
        // Setup dual-source setup required for music crossfading
        GameObject musicGroupA = new GameObject("MusicSource_A");
        GameObject musicGroupB = new GameObject("MusicSource_B");
        musicGroupA.transform.SetParent(transform);
        musicGroupB.transform.SetParent(transform);

        activeMusicSource = musicGroupA.AddComponent<AudioSource>();
        inactiveMusicSource = musicGroupB.AddComponent<AudioSource>();

        activeMusicSource.playOnAwake = false;
        inactiveMusicSource.playOnAwake = false;
    }

    private void InitializeSFXPool()
    {
        sfxPool = new ObjectPool<AudioSource>(
            createFunc: () => {
                GameObject go = new GameObject("PooledSFXSource");
                go.transform.SetParent(transform);
                return go.AddComponent<AudioSource>();
            },
            actionOnGet: (src) => src.gameObject.SetActive(true),
            actionOnRelease: (src) => {
                src.Stop();
                src.gameObject.SetActive(false);
            },
            actionOnDestroy: (src) => Destroy(src.gameObject),
            collectionCheck: false,
            defaultCapacity: 15,
            maxSize: 40
        );
    }

    // ==========================================
    // MUSIC MANAGEMENT (CROSSFADE ENGINE)
    // ==========================================

    public void PlayMusic(AudioCueSO musicCue, float fadeDuration = 1.5f)
    {
        if (musicCue == null || musicCue.clips.Length == 0) return;
        AudioClip clipToPlay = musicCue.GetRandomClip();

        // If the targeted clip is already cleanly running on our primary track, skip restarting it
        if (activeMusicSource.isPlaying && activeMusicSource.clip == clipToPlay) return;

        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        crossfadeCoroutine = StartCoroutine(CrossfadeMusicRoutine(musicCue, clipToPlay, fadeDuration));
    }

    // ==========================================
    // SFX MANAGEMENT (POOLING & MIXING)
    // ==========================================

    public void PlaySFX(AudioCueSO cue)
    {
        if (cue == null) return;
        AudioSource src = sfxPool.Get();
        
        src.spatialBlend = 0f; // Force 2D Spatialization
        ConfigureAndPlaySFX(src, cue);
    }

    public void Play3DSFX(AudioCueSO cue, Vector3 worldPosition)
    {
        if (cue == null) return;
        AudioSource src = sfxPool.Get();

        src.transform.position = worldPosition;
        src.spatialBlend = 1f; // Force 3D Spatialization
        src.rolloffMode = AudioRolloffMode.Logarithmic; // Ensures smooth falloff in distance

        ConfigureAndPlaySFX(src, cue);
    }

    // ==========================================
    // TRACKED DYNAMIC SOUNDS (WITH SMOOTH FADING)
    // ==========================================

    /// <summary>
    /// Spawns a tracked, continuous looping sound that you can update dynamically and fade out smoothly.
    /// </summary>
    public TrackedAudioInstance PlayTrackedLoop(AudioCueSO cue, Transform attachToTransform = null)
    {
        if (cue == null) return null;

        AudioSource src = sfxPool.Get();
        
        src.clip = cue.GetRandomClip();
        src.volume = 0f; // Start silent to allow tracking script to scale it cleanly
        src.pitch = cue.basePitch;
        src.loop = true;
        src.outputAudioMixerGroup = cue.mixerGroup != null ? cue.mixerGroup : defaultSFXGroup;

        if (attachToTransform != null)
        {
            src.transform.SetParent(attachToTransform);
            src.transform.localPosition = Vector3.zero;
            src.spatialBlend = 1f; 
            src.rolloffMode = AudioRolloffMode.Logarithmic;
        }
        else
        {
            src.spatialBlend = 0f; 
        }

        src.Play();

        // Return the controller instance and configure its stop behavior
        return new TrackedAudioInstance(src, cue, (returnedSource, fadeTime) => {
            if (gameObject.activeInHierarchy && returnedSource != null)
            {
                // Hand off the source to a manager coroutine to handle fading out over time
                StartCoroutine(FadeOutAndReleaseTrackedSource(returnedSource, fadeTime));
            }
            else if (returnedSource != null)
            {
                // Fallback: If manager is being destroyed, skip coroutines and recycle immediately
                returnedSource.transform.SetParent(transform);
                sfxPool.Release(returnedSource);
            }
        });
    }

    // ==========================================
    // Private Utility Functions
    // ==========================================

    private void ConfigureAndPlaySFX(AudioSource src, AudioCueSO cue)
    {
        src.clip = cue.GetRandomClip();
        src.volume = cue.volume;
        src.pitch = cue.GetRandomPitch(); // Apply dynamic pitch variations
        src.loop = cue.loop;
        
        // Dynamically assign Custom Mixer Track via ScriptableObject or default backstop
        src.outputAudioMixerGroup = cue.mixerGroup != null ? cue.mixerGroup : defaultSFXGroup;

        src.Play();
        StartCoroutine(ReleaseSFXWhenFinished(src));
    }

    private IEnumerator ReleaseSFXWhenFinished(AudioSource src)
    {
        // Wait while audio track computes execution frames
        yield return new WaitWhile(() => src.isPlaying);
        sfxPool.Release(src);
    }

    private IEnumerator CrossfadeMusicRoutine(AudioCueSO cue, AudioClip clip, float duration)
    {
        // Configure the upcoming silent track
        inactiveMusicSource.clip = clip;
        inactiveMusicSource.loop = cue.loop;
        inactiveMusicSource.pitch = cue.basePitch;
        inactiveMusicSource.outputAudioMixerGroup = cue.mixerGroup != null ? cue.mixerGroup : defaultMusicGroup;
        inactiveMusicSource.volume = 0f;
        inactiveMusicSource.Play();

        float startActiveVol = activeMusicSource.volume;
        float targetInactiveVol = cue.volume;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / duration;

            // Smooth linear volume interpolations
            if (activeMusicSource.isPlaying)
                activeMusicSource.volume = Mathf.Lerp(startActiveVol, 0f, normalizedTime);

            inactiveMusicSource.volume = Mathf.Lerp(0f, targetInactiveVol, normalizedTime);
            yield return null;
        }

        // Clean up states and swap positions
        activeMusicSource.Stop();
        activeMusicSource.volume = 0f;

        AudioSource temp = activeMusicSource;
        activeMusicSource = inactiveMusicSource;
        inactiveMusicSource = temp;
    }

    private IEnumerator FadeOutAndReleaseTrackedSource(AudioSource src, float duration)
    {
        float startVolume = src.volume;
        float timer = 0f;

        while (timer < duration && src != null)
        {
            timer += Time.deltaTime;
            src.volume = Mathf.Lerp(startVolume, 0f, timer / duration);
            yield return null;
        }

        if (src != null)
        {
            src.Stop();
            src.transform.SetParent(transform); // Return anchor back to AudioManager root
            sfxPool.Release(src);
        }
    }
}
