using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class ObjectImpactFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject targetToPunch;
    [SerializeField] private ParticleSystem defaultImpactParticles;
    private ObjectModifierController modifierController;

    [Header("Audio Configurations")]
    [SerializeField] private AudioCueSO defaultBonkSound;
    [SerializeField] private AudioCueSO defaultBinkSound;
    [SerializeField] private AudioCueSO defaultSlideSound;
    [SerializeField] private AudioCueSO aerialSound;

    [Header("Slide & Aerial Audio Smoothing")]
    [SerializeField] private float audioFadeSpeed = 5f;

    [Header("Impact FX Tuning")]
    [SerializeField] private float minImpactForce = 20f;
    [SerializeField] private float minImpactForceForBink = 175f;

    // Components & Physics State
    private Rigidbody rb;
    private int jellyCubeLayer;
    private int exclusionMask;

    // Audio & Loop State
    private const float MAX_SLIDE_VELOCITY = 5f;
    private const float MIN_SLIDE_VELOCITY = 1.5f;
    private TrackedAudioInstance slideAudioInstance;
    private TrackedAudioInstance aerialAudioInstance;
    private readonly List<TrackedAudioInstance> activeLoopingAudioInstances = new List<TrackedAudioInstance>();
    private float targetSlideVolume;
    private float currentSlideVolume;
    private float targetAerialVolume;
    private float currentAerialVolume;

    // Impact & Debounce State
    private const float DEBOUNCE_COOLDOWN = 0.4f;
    private float nextAllowedImpactTime;

    // Custom Particle Runtime Instantiation
    private ParticleSystem spawnedCustomImpactParticles;
    private ParticleSystem lastCustomPrefabReference;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Attempt to auto-fetch if not manually assigned in Inspector
        if (modifierController == null)
        {
            modifierController = GetComponent<ObjectModifierController>();
        }

        jellyCubeLayer = LayerMask.NameToLayer("JellyCube");
        exclusionMask = ~(1 << jellyCubeLayer);
    }

    private void OnEnable()
    {
        if (modifierController != null)
        {
            modifierController.OnPhysicsMaterialUpdated.AddListener(OnPhysicsMaterialUpdated);
        }
    }

    private void OnDisable()
    {
        if (modifierController != null)
        {
            modifierController.OnPhysicsMaterialUpdated.RemoveListener(OnPhysicsMaterialUpdated);
        }
    }

    private void Start()
    {
        UpdateAudioLoops();
    }

    private void OnPhysicsMaterialUpdated(PhysicsMaterial physMat)
    {
        UpdateAudioLoops();
    }

    private void Update()
    {
        float velocityMag = rb != null ? rb.linearVelocity.magnitude : 0f;
        bool isColliding = CheckIfColliding();

        targetSlideVolume = (isColliding && velocityMag > MIN_SLIDE_VELOCITY) ? Mathf.Clamp01(velocityMag / MAX_SLIDE_VELOCITY) : 0f;
        targetAerialVolume = (!isColliding && velocityMag > MIN_SLIDE_VELOCITY) ? Mathf.Clamp01(velocityMag / MAX_SLIDE_VELOCITY) : 0f;

        currentSlideVolume = Mathf.MoveTowards(currentSlideVolume, targetSlideVolume, audioFadeSpeed * Time.deltaTime);
        currentAerialVolume = Mathf.MoveTowards(currentAerialVolume, targetAerialVolume, audioFadeSpeed * Time.deltaTime);

        float pitchScale = modifierController != null ? modifierController.CurrentPitchScale : 1.0f;

        slideAudioInstance?.UpdateParameters(currentSlideVolume, pitchScale);
        aerialAudioInstance?.UpdateParameters(currentAerialVolume, pitchScale);

        foreach (var loopInstance in activeLoopingAudioInstances)
        {
            loopInstance?.UpdateParameters(1.0f, pitchScale);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.impulse.magnitude / Time.fixedDeltaTime > minImpactForce)
        {
            HandleImpact(collision);
        }
    }

    public void HandleImpact(Collision collision)
    {
        if (Time.time < nextAllowedImpactTime) return;

        nextAllowedImpactTime = Time.time + DEBOUNCE_COOLDOWN;

        PlayImpactParticles(collision);
        PlayImpactSounds(collision);
        PlayImpactTween(collision);
    }

    public void UpdateAudioLoops()
    {
        if (AudioManager.Instance == null) return;

        slideAudioInstance?.StopAndRelease();
        slideAudioInstance = null;

        aerialAudioInstance?.StopAndRelease();
        aerialAudioInstance = null;

        foreach (var loop in activeLoopingAudioInstances)
        {
            loop?.StopAndRelease();
        }
        activeLoopingAudioInstances.Clear();

        AudioCueSO activeSlide = (modifierController != null && modifierController.ActiveSlideSound != null) 
            ? modifierController.ActiveSlideSound 
            : defaultSlideSound;

        if (activeSlide != null)
        {
            slideAudioInstance = AudioManager.Instance.PlayTrackedLoop(activeSlide, transform);
        }

        if (aerialSound != null)
        {
            aerialAudioInstance = AudioManager.Instance.PlayTrackedLoop(aerialSound, transform);
        }

        if (modifierController != null)
        {
            foreach (var loopCue in modifierController.ActiveLoopingSoundEffects)
            {
                if (loopCue == null) continue;
                TrackedAudioInstance instance = AudioManager.Instance.PlayTrackedLoop(loopCue, transform);
                if (instance != null)
                {
                    activeLoopingAudioInstances.Add(instance);
                }
            }
        }
    }

    private void PlayImpactTween(Collision collision)
    {
        if (targetToPunch == null) return;

        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
        float randomMagnitude = (impactForce < minImpactForceForBink) ? 0.1f : 0.3f;

        targetToPunch.transform.DOKill(true);
        targetToPunch.transform.localScale = Vector3.one;

        Vector3 punchStrength = (Vector3.one + Random.insideUnitSphere * randomMagnitude)
            * Mathf.Clamp(collision.relativeVelocity.magnitude * 0.05f, 0.05f, 0.3f);

        GameObject renderMask = modifierController != null ? modifierController.RenderMask : null;

        // If no render mask is assigned, allow punch animation by default
        bool canPunch = renderMask == null || renderMask.activeSelf;

        if (canPunch)
        {
            Vector3 cachedScale = transform.localScale;
            targetToPunch.transform.DOPunchScale(punchStrength, duration: 0.35f, vibrato: 10, elasticity: 1f)
                .OnComplete(() => transform.localScale = cachedScale);
        }
    }

    private void PlayImpactParticles(Collision collision)
    {
        if (modifierController != null && modifierController.AreImpactParticlesDisabled) return;
        if (collision.contacts.Length == 0) return;

        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
        if (impactForce < minImpactForceForBink) return;

        ParticleSystem targetParticle = defaultImpactParticles;

        if (modifierController != null && modifierController.CustomImpactParticlePrefab != null)
        {
            ParticleSystem customPrefab = modifierController.CustomImpactParticlePrefab;
            if (spawnedCustomImpactParticles == null || lastCustomPrefabReference != customPrefab)
            {
                if (spawnedCustomImpactParticles != null)
                {
                    Destroy(spawnedCustomImpactParticles.gameObject);
                }
                lastCustomPrefabReference = customPrefab;
                spawnedCustomImpactParticles = Instantiate(customPrefab);
            }
            targetParticle = spawnedCustomImpactParticles;
        }

        if (targetParticle != null)
        {
            targetParticle.transform.position = collision.contacts[0].point;
            targetParticle.Play();
        }
    }

    private void PlayImpactSounds(Collision collision)
    {
        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;

        AudioCueSO bonk = (modifierController != null && modifierController.ActiveBonkSound != null) 
            ? modifierController.ActiveBonkSound 
            : defaultBonkSound;

        AudioCueSO bink = (modifierController != null && modifierController.ActiveBinkSound != null) 
            ? modifierController.ActiveBinkSound 
            : defaultBinkSound;

        AudioCueSO targetCue = (impactForce > minImpactForceForBink) ? bink : bonk;

        if (targetCue != null)
        {
            float pitchScale = modifierController != null ? modifierController.CurrentPitchScale : 1.0f;
            AudioCueSO tempCue = ScriptableObject.Instantiate(targetCue);
            tempCue.basePitch = targetCue.GetRandomPitch(pitchScale);
            AudioManager.Instance.Play3DSFX(tempCue, transform.position);
        }
    }

    public bool CheckIfColliding()
    {
        float radius = transform.localScale.x;
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius, exclusionMask, QueryTriggerInteraction.Ignore);
        return hitColliders.Length > 1;
    }

    public void CleanUpClonedParticles()
    {
        if (spawnedCustomImpactParticles != null)
        {
            Destroy(spawnedCustomImpactParticles.gameObject);
            spawnedCustomImpactParticles = null;
            lastCustomPrefabReference = null;
        }
    }
}