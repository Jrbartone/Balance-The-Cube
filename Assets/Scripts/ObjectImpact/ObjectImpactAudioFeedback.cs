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
    [SerializeField] private float audioFadeSpeed = 5f; // Speed at which audio fades in/out

    private ObjectImpactBroadcaster broadcaster;
    private ObjectModifierController modifierController;

    private TrackedAudioInstance slideAudioInstance;
    private TrackedAudioInstance aerialAudioInstance;
    private readonly List<TrackedAudioInstance> activeLoopingAudioInstances = new List<TrackedAudioInstance>();

    // Current and target volumes for smooth fading
    private float currentSlideVolume;
    private float targetSlideVolume;
    private float currentAerialVolume;
    private float targetAerialVolume;

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
            broadcaster.OnStateTransition.AddListener(OnStateTransition);
            broadcaster.OnStateTick.AddListener(OnStateTick);
        }

        if (modifierController == null && broadcaster != null)
        {
            modifierController = broadcaster.ModifierController;
        }
    }

    private void OnDisable()
    {
        if (broadcaster != null)
        {
            broadcaster.OnImpact.RemoveListener(OnImpactHandled);
            broadcaster.OnStateTransition.RemoveListener(OnStateTransition);
            broadcaster.OnStateTick.RemoveListener(OnStateTick);
        }
    }

    private void Start()
    {
        UpdateAudioLoops();
    }

    private void Update()
    {
        float pitchScale = modifierController != null ? modifierController.CurrentPitchScale : 1.0f;

        // Smoothly fade slide and aerial volumes toward their targets
        currentSlideVolume = Mathf.MoveTowards(currentSlideVolume, targetSlideVolume, audioFadeSpeed * Time.deltaTime);
        currentAerialVolume = Mathf.MoveTowards(currentAerialVolume, targetAerialVolume, audioFadeSpeed * Time.deltaTime);

        // Apply parameters to persistent loop instances
        slideAudioInstance?.UpdateParameters(currentSlideVolume, pitchScale);
        aerialAudioInstance?.UpdateParameters(currentAerialVolume, pitchScale);

        // Update other custom looping audio effect parameters
        for (int i = 0; i < activeLoopingAudioInstances.Count; i++)
        {
            activeLoopingAudioInstances[i]?.UpdateParameters(1.0f, pitchScale);
        }
    }

    private void OnPhysicsMaterialUpdated(PhysicsMaterial physMat)
    {
        UpdateAudioLoops();
    }

    private void OnStateTransition(ObjectImpactBroadcaster.MovementState oldState, ObjectImpactBroadcaster.MovementState newState, float finalTimeInOldState)
    {
        // Set target volumes to 0 when leaving states
        if (oldState == ObjectImpactBroadcaster.MovementState.Slide)
        {
            targetSlideVolume = 0f;
        }
        else if (oldState == ObjectImpactBroadcaster.MovementState.Air)
        {
            targetAerialVolume = 0f;
        }

        if (newState == ObjectImpactBroadcaster.MovementState.None)
        {
            targetSlideVolume = 0f;
            targetAerialVolume = 0f;
        }
    }

    private void OnStateTick(ObjectImpactBroadcaster.MovementState state, float timeInState, float volume)
    {
        // Set target volumes to full when actively in the state
        if (state == ObjectImpactBroadcaster.MovementState.Slide)
        {
            targetSlideVolume = volume;
            targetAerialVolume = 0f;
        }
        else if (state == ObjectImpactBroadcaster.MovementState.Air)
        {
            targetAerialVolume = volume;
            targetSlideVolume = 0f;
        }
        else
        {
            targetSlideVolume = 0f;
            targetAerialVolume = 0f;
        }
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

        // Clean up old instances if resetting
        slideAudioInstance?.StopAndRelease();
        slideAudioInstance = null;

        aerialAudioInstance?.StopAndRelease();
        aerialAudioInstance = null;

        foreach (var loop in activeLoopingAudioInstances) loop?.StopAndRelease();
        activeLoopingAudioInstances.Clear();

        // Reset volumes
        currentSlideVolume = 0f;
        targetSlideVolume = 0f;
        currentAerialVolume = 0f;
        targetAerialVolume = 0f;

        AudioCueSO activeSlide = (modifierController != null && modifierController.ActiveSlideSound != null) 
            ? modifierController.ActiveSlideSound 
            : defaultSlideSound;

        // Initialize persistent loop instances starting at volume 0
        if (activeSlide != null) slideAudioInstance = AudioManager.Instance.PlayTrackedLoop(activeSlide, transform);
        if (aerialSound != null) aerialAudioInstance = AudioManager.Instance.PlayTrackedLoop(aerialSound, transform);

        if (slideAudioInstance != null) slideAudioInstance.UpdateParameters(0f, 1f);
        if (aerialAudioInstance != null) aerialAudioInstance.UpdateParameters(0f, 1f);

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