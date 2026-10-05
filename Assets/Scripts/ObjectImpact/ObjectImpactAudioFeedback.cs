using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ObjectImpactBroadcaster))]
public class ObjectImpactAudioFeedback : MonoBehaviour
{
    [Header("Audio Configurations")]
    [SerializeField] private AudioCueSO defaultBonkSound;
    [SerializeField] private AudioCueSO defaultBinkSound;
    [SerializeField] private AudioCueSO defaultSlideSound;
    [SerializeField] private AudioCueSO aerialSound;

    private ObjectImpactBroadcaster broadcaster;
    private ObjectModifierController modifierController;

    private TrackedAudioInstance slideAudioInstance;
    private TrackedAudioInstance aerialAudioInstance;
    private readonly List<TrackedAudioInstance> activeLoopingAudioInstances = new List<TrackedAudioInstance>();

    private void Awake()
    {
        broadcaster = GetComponent<ObjectImpactBroadcaster>();
        modifierController = broadcaster != null ? broadcaster.ModifierController : GetComponent<ObjectModifierController>();
    }

    private void OnEnable()
    {
        if (broadcaster != null)
        {
            broadcaster.OnImpact.AddListener(OnImpactHandled);
            broadcaster.OnSlide.AddListener(OnSlideUpdated);
            broadcaster.OnAir.AddListener(OnAirUpdated);
        }

        // Re-check modifierController in case of Awake initialization order differences
        if (modifierController == null && broadcaster != null)
        {
            modifierController = broadcaster.ModifierController;
        }

        if (modifierController != null)
        {
            modifierController.OnPhysicsMaterialUpdated.AddListener(OnPhysicsMaterialUpdated);
        }
    }

    private void OnDisable()
    {
        if (broadcaster != null)
        {
            broadcaster.OnImpact.RemoveListener(OnImpactHandled);
            broadcaster.OnSlide.RemoveListener(OnSlideUpdated);
            broadcaster.OnAir.RemoveListener(OnAirUpdated);
        }

        if (modifierController != null)
        {
            modifierController.OnPhysicsMaterialUpdated.RemoveListener(OnPhysicsMaterialUpdated);
        }
    }

    private void Start()
    {
        UpdateAudioLoops();
    }

    private void Update()
    {
        // Keep active looping sound effect parameters/pitch updated each frame
        float pitchScale = modifierController != null ? modifierController.CurrentPitchScale : 1.0f;
        for (int i = 0; i < activeLoopingAudioInstances.Count; i++)
        {
            activeLoopingAudioInstances[i]?.UpdateParameters(1.0f, pitchScale);
        }
    }

    private void OnPhysicsMaterialUpdated(PhysicsMaterial physMat)
    {
        UpdateAudioLoops();
    }

    private void OnSlideUpdated(float volume)
    {
        float pitchScale = modifierController != null ? modifierController.CurrentPitchScale : 1.0f;
        slideAudioInstance?.UpdateParameters(volume, pitchScale);
    }

    private void OnAirUpdated(float volume)
    {
        float pitchScale = modifierController != null ? modifierController.CurrentPitchScale : 1.0f;
        aerialAudioInstance?.UpdateParameters(volume, pitchScale);
    }

    private void OnImpactHandled(Collision collision, ObjectImpactBroadcaster.ImpactType impactType)
    {
        PlayImpactSounds(impactType);
    }

    private void PlayImpactSounds(ObjectImpactBroadcaster.ImpactType impactType)
    {
        AudioCueSO bonk = (modifierController != null && modifierController.ActiveBonkSound != null) 
            ? modifierController.ActiveBonkSound 
            : defaultBonkSound;

        AudioCueSO bink = (modifierController != null && modifierController.ActiveBinkSound != null) 
            ? modifierController.ActiveBinkSound 
            : defaultBinkSound;

        AudioCueSO targetCue = (impactType == ObjectImpactBroadcaster.ImpactType.Bink) ? bink : bonk;

        if (targetCue != null)
        {
            float pitchScale = modifierController != null ? modifierController.CurrentPitchScale : 1.0f;
            AudioCueSO tempCue = ScriptableObject.Instantiate(targetCue);
            tempCue.basePitch = targetCue.GetRandomPitch(pitchScale);
            AudioManager.Instance.Play3DSFX(tempCue, transform.position);
        }
    }

    public void UpdateAudioLoops()
    {
        if (AudioManager.Instance == null) return;

        slideAudioInstance?.StopAndRelease();
        slideAudioInstance = null;

        aerialAudioInstance?.StopAndRelease();
        aerialAudioInstance = null;

        foreach (var loop in activeLoopingAudioInstances) loop?.StopAndRelease();
        activeLoopingAudioInstances.Clear();

        AudioCueSO activeSlide = (modifierController != null && modifierController.ActiveSlideSound != null) 
            ? modifierController.ActiveSlideSound 
            : defaultSlideSound;

        if (activeSlide != null) slideAudioInstance = AudioManager.Instance.PlayTrackedLoop(activeSlide, transform);
        if (aerialSound != null) aerialAudioInstance = AudioManager.Instance.PlayTrackedLoop(aerialSound, transform);

        if (modifierController != null)
        {
            foreach (var loopCue in modifierController.ActiveLoopingSoundEffects)
            {
                if (loopCue == null) continue;
                TrackedAudioInstance instance = AudioManager.Instance.PlayTrackedLoop(loopCue, transform);
                if (instance != null) activeLoopingAudioInstances.Add(instance);
            }
        }
    }
}