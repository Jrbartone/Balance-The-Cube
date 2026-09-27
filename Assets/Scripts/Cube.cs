using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;

public class Cube : MonoBehaviour
{
    [Header("References")]
    public GameObject renderedCube;
    public ParticleSystem impactParticles;
    public GameObject renderMask;
    public GameObject iceSlime;

    [Header("Modifiers")]
    [SerializeField] private List<CubeModifier> modifiers = new List<CubeModifier>();

    [Header("Audio Configurations")]
    [SerializeField] private AudioCueSO defaultBonkSound;
    [SerializeField] private AudioCueSO defaultBinkSound;
    [SerializeField] private AudioCueSO defaultSlideSound;
    [SerializeField] private AudioCueSO aerialSound;

    [Header("Slide & Aerial Audio Smoothing")]
    [Tooltip("Adjust to speed up or slow down the fade duration")]
    [SerializeField] private float audioFadeSpeed = 5f;

    [Header("Impact FX Tuning")]
    [Tooltip("Force threshold for initial impact detection")]
    [SerializeField] private float minImpactForce = 20f;
    [Tooltip("Force threshold for high-impact effects (Bink sound, particles, stronger scale punch)")]
    [SerializeField] private float minImpactForceForBink = 175f;

    // Component References
    private Rigidbody rb;
    private Renderer cubeRenderer;
    private Collider cubeCollider;
    private Material defaultMaterial;

    // Slide & Loop Audio State
    private const float MAX_SLIDE_VELOCITY = 5f;
    private const float MIN_SLIDE_VELOCITY = 1.5f;
    private TrackedAudioInstance slideAudioInstance;
    private TrackedAudioInstance aerialAudioInstance;
    private readonly List<TrackedAudioInstance> activeLoopingAudioInstances = new List<TrackedAudioInstance>();
    private readonly List<AudioCueSO> activeLoopingSoundEffects = new List<AudioCueSO>();
    private float targetSlideVolume;
    private float currentSlideVolume;
    private float targetAerialVolume;
    private float currentAerialVolume;
    private int jellyCubeLayer;
    private int exclusionMask;

    // Impact & Debounce State
    private const float DEBOUNCE_COOLDOWN = 0.4f;
    private float nextAllowedImpactTime;

    // Modifier Runtime State
    private AudioCueSO activeBonkSound;
    private AudioCueSO activeBinkSound;
    private AudioCueSO activeSlideSound;
    private int highestAudioPriority = int.MinValue;

    // Impact Particle State
    private ParticleSystem activeImpactParticles;
    private ParticleSystem spawnedCustomImpactParticles;
    private bool areImpactParticlesDisabled;
    private int highestParticlePriority = int.MinValue;

    private readonly List<GameObject> activeEffectInstances = new List<GameObject>();
    private int multiCubeCount = 1;
    private float currentPitchScale = 1.0f;
    private float negativeFrictionBoost;
    private Vector3 cachedLocalScale = Vector3.one;

    // Reset Constants
    private const float BASE_MASS = 1.0f;
    private const float BASE_STATIC_FRICTION = 0f;
    private const float BASE_DYNAMIC_FRICTION = 0f;
    private const float BASE_BOUNCINESS = 0.3f;
    private static readonly Vector3 BASE_SCALE = Vector3.one;

    public int MultiCubeCount => multiCubeCount;

    #region Unity Lifecycle

    private void Awake()
    {
        jellyCubeLayer = LayerMask.NameToLayer("JellyCube");
        exclusionMask = ~(1 << jellyCubeLayer);
        rb = GetComponent<Rigidbody>();
        cubeCollider = GetComponent<Collider>();

        if (renderedCube != null && renderedCube.TryGetComponent(out cubeRenderer))
        {
            defaultMaterial = cubeRenderer.sharedMaterial;
        }
    }

    private void Start()
    {
        transform.rotation = Random.rotation;
        ReapplyModifiers();
        cachedLocalScale = transform.localScale;

        if (MultiCubeCount > 1)
        {
            SpawnAdditionalCubes();
        }
    }

    private void Update()
    {
        float velocityMag = rb != null ? rb.linearVelocity.magnitude : 0f;
        bool isColliding = CheckIfColliding();

        // Calculate target volumes based on velocity and ground contact
        targetSlideVolume = (isColliding && velocityMag > MIN_SLIDE_VELOCITY) ? Mathf.Clamp01(velocityMag / MAX_SLIDE_VELOCITY) : 0f;
        targetAerialVolume = (!isColliding && velocityMag > MIN_SLIDE_VELOCITY) ? Mathf.Clamp01(velocityMag / MAX_SLIDE_VELOCITY) : 0f;

        // Smoothly interpolate audio volumes
        currentSlideVolume = Mathf.MoveTowards(currentSlideVolume, targetSlideVolume, audioFadeSpeed * Time.deltaTime);
        currentAerialVolume = Mathf.MoveTowards(currentAerialVolume, targetAerialVolume, audioFadeSpeed * Time.deltaTime);

        // Update active tracks
        slideAudioInstance?.UpdateParameters(currentSlideVolume, currentPitchScale);
        aerialAudioInstance?.UpdateParameters(currentAerialVolume, currentPitchScale);

        foreach (var loopInstance in activeLoopingAudioInstances)
        {
            loopInstance?.UpdateParameters(1.0f, currentPitchScale);
        }
    }

    #endregion

    #region Physics & Collision Handlers

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.impulse.magnitude / Time.fixedDeltaTime > minImpactForce)
        {
            OnImpact(collision);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (negativeFrictionBoost > 0f && collision.contacts.Length > 0)
        {
            Vector3 surfaceNormal = collision.contacts[0].normal;
            Vector3 slideDirection = Vector3.ProjectOnPlane(rb.linearVelocity, surfaceNormal).normalized;
            rb.AddForce(slideDirection * negativeFrictionBoost, ForceMode.Acceleration);
        }
    }

    private void OnImpact(Collision collision)
    {
        if (Time.time < nextAllowedImpactTime) return;

        nextAllowedImpactTime = Time.time + DEBOUNCE_COOLDOWN;

        PlayImpactParticles(collision);
        PlayImpactSounds(collision);
        PlayImpactTween(collision);
    }

    #endregion

    #region Visual & Audio Effects

    private void PlayImpactTween(Collision collision)
    {
        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
        float randomMagnitude = (impactForce < minImpactForceForBink) ? 0.1f : 0.3f;

        renderedCube.transform.DOKill(true);
        renderedCube.transform.localScale = Vector3.one;

        Vector3 punchStrength = (Vector3.one + Random.insideUnitSphere * randomMagnitude)
            * Mathf.Clamp(collision.relativeVelocity.magnitude * 0.05f, 0.05f, 0.3f);

        if (renderMask != null && renderMask.activeSelf)
        {
            renderedCube.transform.DOPunchScale(punchStrength, duration: 0.35f, vibrato: 10, elasticity: 1f)
                .OnComplete(() => transform.localScale = cachedLocalScale);
        }
    }

    private void PlayImpactParticles(Collision collision)
    {
        if (areImpactParticlesDisabled || activeImpactParticles == null || collision.contacts.Length == 0) return;

        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
        if (impactForce < minImpactForceForBink) return;

        activeImpactParticles.transform.position = collision.contacts[0].point;
        activeImpactParticles.Play();
    }

    private void PlayImpactSounds(Collision collision)
    {
        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
        AudioCueSO targetCue = (impactForce > minImpactForceForBink) ? activeBinkSound : activeBonkSound;

        if (targetCue != null)
        {
            AudioCueSO tempCue = ScriptableObject.Instantiate(targetCue);
            tempCue.basePitch = targetCue.GetRandomPitch(currentPitchScale);
            AudioManager.Instance.Play3DSFX(tempCue, transform.position);
        }
    }

    private void UpdateJellyMaterialIfPresent(PhysicsMaterial physicsMat = null)
    {
        foreach (GameObject fxInstance in activeEffectInstances)
        {
            if (fxInstance == null) continue;

            var jellyComponent = fxInstance.GetComponentInChildren<JellyRigSimulator>();
            if (jellyComponent != null)
            {
                jellyComponent.UpdateJellyMaterial(physicsMat);
                break;
            }
        }
    }

    #endregion

    #region Modifier API & Logic

    public void AddModifier(CubeModifier modifier)
    {
        if (modifier == null) return;

        if (modifier is CubeMaterialModifer)
        {
            modifiers.RemoveAll(m => m is CubeMaterialModifer);
        }

        modifiers.Add(modifier);
        ReapplyModifiers();
    }

    public void RemoveModifier(CubeModifier modifier)
    {
        if (modifier != null && modifiers.Remove(modifier))
        {
            ReapplyModifiers();
        }
    }

    public void ClearModifiers()
    {
        modifiers.Clear();
        ReapplyModifiers();
    }

    public List<Cube> SpawnAdditionalCubes()
    {
        List<Cube> spawnedCubes = new List<Cube>();
        int extraCubesToSpawn = multiCubeCount - 1;

        CinemachineTargetGroup targetGroup = FindFirstObjectByType<CinemachineTargetGroup>();

        float minRadius = BASE_SCALE.x * 0.5f * Mathf.Sqrt(3f);
        float maxRadius = BASE_SCALE.x * 4f;

        for (int i = 0; i < extraCubesToSpawn; i++)
        {
            Vector3 direction = Random.insideUnitSphere;
            direction.y = Mathf.Abs(direction.y);
            if (direction == Vector3.zero) direction = Vector3.up;
            direction.Normalize();

            Vector3 offset = direction * Random.Range(minRadius, maxRadius);
            Cube newCube = Instantiate(this, transform.position + offset, Quaternion.identity);

            newCube.CleanUpClonedFX();
            newCube.iceSlime = this.iceSlime;
            newCube.modifiers = new List<CubeModifier>();

            foreach (var mod in this.modifiers)
            {
                if (mod == null) continue;
                CubeModifier modInstance = Instantiate(mod);
                modInstance.multi = 0;
                newCube.modifiers.Add(modInstance);
            }

            newCube.ReapplyModifiers();
            spawnedCubes.Add(newCube);

            if (targetGroup != null)
            {
                targetGroup.AddMember(newCube.transform, weight: 1f, radius: newCube.transform.localScale.x * 0.5f);
            }
        }

        return spawnedCubes;
    }

    private void CleanUpClonedFX()
    {
        foreach (var fx in activeEffectInstances)
        {
            if (fx != null) Destroy(fx);
        }
        activeEffectInstances.Clear();

        if (spawnedCustomImpactParticles != null)
        {
            Destroy(spawnedCustomImpactParticles.gameObject);
            spawnedCustomImpactParticles = null;
        }

        Transform parentTransform = renderedCube != null ? renderedCube.transform : transform;
        for (int i = parentTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = parentTransform.GetChild(i);
            if (child.gameObject == renderMask) continue;
            Destroy(child.gameObject);
        }
    }

    private void ReapplyModifiers()
    {
        List<CubeModifier> processedModifiers = new List<CubeModifier>();
        HashSet<CubeModifier> seenNonStackableModifiers = new HashSet<CubeModifier>();

        foreach (var mod in modifiers)
        {
            if (mod == null) continue;
            if (!mod.canStack && !seenNonStackableModifiers.Add(mod)) continue;

            processedModifiers.Add(mod);
        }

        if (renderMask != null) renderMask.SetActive(true);

        // Reset visual effects & scale/mass
        foreach (var fx in activeEffectInstances)
        {
            if (fx != null) Destroy(fx);
        }
        activeEffectInstances.Clear();

        if (spawnedCustomImpactParticles != null)
        {
            Destroy(spawnedCustomImpactParticles.gameObject);
            spawnedCustomImpactParticles = null;
        }

        transform.localScale = BASE_SCALE;
        if (rb != null) rb.mass = BASE_MASS;

        // Reset Audio, Particle, & Material Defaults
        activeBonkSound = defaultBonkSound;
        activeBinkSound = defaultBinkSound;
        activeSlideSound = defaultSlideSound;
        highestAudioPriority = int.MinValue;
        activeLoopingSoundEffects.Clear();

        activeImpactParticles = impactParticles;
        areImpactParticlesDisabled = false;
        highestParticlePriority = int.MinValue;

        if (cubeRenderer != null && defaultMaterial != null)
        {
            cubeRenderer.material = defaultMaterial;
        }

        // Check for Ice and Slime modifiers
        bool hasIce = false;
        bool hasSlime = false;

        foreach (var mod in processedModifiers)
        {
            string modName = mod.name.ToLower();
            if (modName.Contains("ice")) hasIce = true;
            if (modName.Contains("slime")) hasSlime = true;
        }

        bool hasBothIceAndSlime = hasIce && hasSlime;
        Transform parentTransform = renderedCube != null ? renderedCube.transform : transform;

        if (hasBothIceAndSlime && iceSlime != null)
        {
            activeEffectInstances.Add(Instantiate(iceSlime, parentTransform));
        }

        // Accumulate levels and evaluate overrides
        int totalWeightLevel = 0, totalStaticFrictionLevel = 0, totalDynamicFrictionLevel = 0;
        int totalBouncinessLevel = 0, totalSizeLevel = 0, totalMultiLevel = 0;

        CubeMaterialModifer activeMaterialModifier = null;
        GameObject customImpactPrefabToSpawn = null;

        foreach (var mod in processedModifiers)
        {
            if (mod.disableRenderMask && renderMask != null)
            {
                renderMask.SetActive(false);
            }

            totalWeightLevel += mod.weight;
            totalStaticFrictionLevel += mod.staticFriction;
            totalDynamicFrictionLevel += mod.dynamicFriction;
            totalBouncinessLevel += mod.bounciness;
            totalSizeLevel += mod.size;
            totalMultiLevel += mod.multi;

            if (mod is CubeEffectModifier effectMod)
            {
                if (effectMod.visualEffectPrefab != null)
                {
                    string modName = mod.name.ToLower();
                    bool isIceOrSlime = modName.Contains("ice") || modName.Contains("slime");

                    if (!hasBothIceAndSlime || !isIceOrSlime)
                    {
                        activeEffectInstances.Add(Instantiate(effectMod.visualEffectPrefab, parentTransform));
                    }
                }

                if (effectMod.loopingSoundEffect != null)
                {
                    activeLoopingSoundEffects.Add(effectMod.loopingSoundEffect);
                }

                if (effectMod.impactParticlePriority >= highestParticlePriority)
                {
                    highestParticlePriority = effectMod.impactParticlePriority;
                    areImpactParticlesDisabled = effectMod.disableImpactParticles;

                    if (effectMod.impactParticlePrefab != null)
                    {
                        customImpactPrefabToSpawn = effectMod.impactParticlePrefab;
                    }
                }
            }

            if (mod is CubeMaterialModifer matMod)
            {
                activeMaterialModifier = matMod;
            }

            bool hasAudioOverride = mod.bonkSoundOverride != null || mod.binkSoundOverride != null || mod.slideSoundOverride != null;
            if (hasAudioOverride && mod.soundOverridePriority >= highestAudioPriority)
            {
                highestAudioPriority = mod.soundOverridePriority;
                if (mod.bonkSoundOverride != null) activeBonkSound = mod.bonkSoundOverride;
                if (mod.binkSoundOverride != null) activeBinkSound = mod.binkSoundOverride;
                if (mod.slideSoundOverride != null) activeSlideSound = mod.slideSoundOverride;
            }
        }

        if (customImpactPrefabToSpawn != null)
        {
            GameObject spawnedParticleObject = Instantiate(customImpactPrefabToSpawn);
            if (spawnedParticleObject.TryGetComponent(out spawnedCustomImpactParticles))
            {
                activeImpactParticles = spawnedCustomImpactParticles;
            }
        }

        // Clamp combined modifier levels to [-7, 7]
        totalWeightLevel = Mathf.Clamp(totalWeightLevel, -7, 7);
        totalStaticFrictionLevel = Mathf.Clamp(totalStaticFrictionLevel, -7, 7);
        totalDynamicFrictionLevel = Mathf.Clamp(totalDynamicFrictionLevel, -7, 7);
        totalBouncinessLevel = Mathf.Clamp(totalBouncinessLevel, -7, 7);
        totalSizeLevel = Mathf.Clamp(totalSizeLevel, -7, 7);
        totalMultiLevel = Mathf.Clamp(totalMultiLevel, -7, 7);

        negativeFrictionBoost = (totalDynamicFrictionLevel < 0) ? Mathf.Abs(totalDynamicFrictionLevel) * 0.09f : 0f;

        // Apply Material and Sound Fallbacks
        if (activeMaterialModifier != null)
        {
            if (cubeRenderer != null && activeMaterialModifier.material != null)
            {
                cubeRenderer.material = activeMaterialModifier.material;
            }

            if (activeBonkSound == defaultBonkSound && activeMaterialModifier.bonkSound != null)
            {
                activeBonkSound = activeMaterialModifier.bonkSound;
            }
            if (activeBinkSound == defaultBinkSound && activeMaterialModifier.binkSound != null)
            {
                activeBinkSound = activeMaterialModifier.binkSound;
            }
        }

        UpdateAudioLoops();

        // Apply Calculated Parameters
        multiCubeCount = Mathf.Max(1, 1 + totalMultiLevel);

        if (rb != null)
        {
            rb.mass = Mathf.Clamp(BASE_MASS * Mathf.Pow(1.5f, totalWeightLevel), 0.05f, 15f);
        }

        transform.localScale = BASE_SCALE * Mathf.Pow(1.2f, totalSizeLevel);
        cachedLocalScale = transform.localScale;
        currentPitchScale = Mathf.Pow(1.2f, -totalSizeLevel);

        PhysicsMaterial physMat = null;
        if (cubeCollider != null)
        {
            physMat = new PhysicsMaterial("CubeDynamicPhysicsMat")
            {
                staticFriction = Mathf.Clamp(BASE_STATIC_FRICTION + (totalStaticFrictionLevel * 0.14f), 0f, 1.0f),
                dynamicFriction = Mathf.Clamp(BASE_DYNAMIC_FRICTION + (totalDynamicFrictionLevel * 0.14f), 0f, 1.0f),
                bounciness = Mathf.Clamp(BASE_BOUNCINESS + (totalBouncinessLevel * 0.1f), 0.01f, 0.99f),
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            cubeCollider.material = physMat;
        }

        UpdateJellyMaterialIfPresent(physMat);
    }

    private void UpdateAudioLoops()
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

        if (activeSlideSound != null)
        {
            slideAudioInstance = AudioManager.Instance.PlayTrackedLoop(activeSlideSound, transform);
        }

        if (aerialSound != null)
        {
            aerialAudioInstance = AudioManager.Instance.PlayTrackedLoop(aerialSound, transform);
        }

        foreach (var loopCue in activeLoopingSoundEffects)
        {
            if (loopCue == null) continue;
            TrackedAudioInstance instance = AudioManager.Instance.PlayTrackedLoop(loopCue, transform);
            if (instance != null)
            {
                activeLoopingAudioInstances.Add(instance);
            }
        }
    }

    public bool CheckIfColliding()
    {
        float radius = transform.localScale.x;
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius, exclusionMask, QueryTriggerInteraction.Ignore);
        return hitColliders.Length > 1;
    }

    #endregion
}