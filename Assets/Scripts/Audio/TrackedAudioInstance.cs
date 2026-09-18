using UnityEngine;

public class TrackedAudioInstance
{
    public AudioSource Source { get; private set; }
    private AudioCueSO originalCue;
    // Updated delegate signature to pass back the fade request duration
    private System.Action<AudioSource, float> releaseAction;
    private bool isFadingOut = false;

    public TrackedAudioInstance(AudioSource source, AudioCueSO cue, System.Action<AudioSource, float> onRelease)
    {
        Source = source;
        originalCue = cue;
        releaseAction = onRelease;
    }

    /// <summary>
    /// Updates the runtime volume and pitch based on dynamic parameters (e.g., speed, RPM).
    /// </summary>
    public void UpdateParameters(float volumePercent, float pitchPercent)
    {
        // Prevent game scripts from fighting the fadeout volume if it has already been stopped
        if (Source == null || !Source.isPlaying || isFadingOut) return;
        
        Source.volume = Mathf.Clamp01(originalCue.volume * volumePercent);
        Source.pitch = Mathf.Clamp(originalCue.basePitch * pitchPercent, 0.1f, 3f);
    }

    /// <summary>
    /// Smoothly fades out the volume and returns the AudioSource back to the global pool.
    /// </summary>
    /// <param name="fadeDuration">Time in seconds to transition down to complete silence.</param>
    public void StopAndRelease(float fadeDuration = 0.5f)
    {
        if (Source == null || isFadingOut) return;
        
        isFadingOut = true;
        releaseAction?.Invoke(Source, fadeDuration);
        Source = null; // Yield safety to prevent further modifications from this reference handle
    }
}
