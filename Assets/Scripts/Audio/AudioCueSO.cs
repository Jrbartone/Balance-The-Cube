using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "NewAudioCue", menuName = "Audio/Audio Cue")]
public class AudioCueSO : ScriptableObject
{
    [Header("Audio Tracks")]
    public AudioMixerGroup mixerGroup;

    [Header("Clip Variations")]
    [Tooltip("If multiple clips are provided, one will be chosen at random per play call.")]
    public AudioClip[] clips;

    [Header("Settings")]
    [Range(0f, 1f)] public float volume = 1f;
    public bool loop = false;

    [Header("Pitch Randomization")]
    [Range(0.1f, 2f)] public float basePitch = 1f;
    [Range(0f, 0.5f)] public float pitchRandomRange = 0f;

    /// <summary>
    /// Evaluates data settings and returns a valid clip.
    /// </summary>
    public AudioClip GetRandomClip()
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }

    /// <summary>
    /// Calculates a pitch centered on basePitch shifted by the random variation range.
    /// </summary>
    public float GetRandomPitch(float pitchScale = 1f)
    {
        float calculatedPitch = basePitch;
        if (pitchRandomRange > 0f)
        {
            calculatedPitch += Random.Range(-pitchRandomRange, pitchRandomRange);
        }
        return calculatedPitch * pitchScale;
    }
}
