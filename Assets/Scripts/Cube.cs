using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;

public class Cube : MonoBehaviour
{
    [Header("References")]
    public GameObject renderedCube;
    public ParticleSystem impactParticles;

    [Header("Modifiers")]
    [SerializeField] private List<CubeModifier> modifiers = new List<CubeModifier>();

    [Header("Audio Configurations")]
    [SerializeField] private AudioCueSO defaultBonkSound;
    [SerializeField] private AudioCueSO defaultBinkSound;
    [SerializeField] private AudioCueSO defaultSlideSound;

    [Header("Slide Audio Smoothing")]
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

    // Slide Audio State
    private const float MAX_SLIDE_VELOCITY = 5f;
    private const float MIN_SLIDE_VELOCITY = 0.005f;
    private TrackedAudioInstance slideAudioInstance;
    private int collidedObjects;
    private float targetSlideVolume;
    private float currentSlideVolume;

    // Impact & Debounce State
    private const float DEBOUNCE_COOLDOWN = 0.4f;
    private float nextAllowedImpactTime;

    // Modifier Runtime State
    private AudioCueSO activeBonkSound;
    private AudioCueSO activeBinkSound;
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
        rb = GetComponent<Rigidbody>();
        cubeCollider = GetComponent<Collider>();

        if (renderedCube != null)
        {
            cubeRenderer = renderedCube.GetComponent<Renderer>();
            if (cubeRenderer != null)
            {
                defaultMaterial = cubeRenderer.sharedMaterial;
            }
        }
    }

    private void Start()
    {
        transform.rotation = Random.rotation;
        slideAudioInstance = AudioManager.Instance.PlayTrackedLoop(defaultSlideSound, transform);

        // Apply any modifiers configured in the Inspector
        ReapplyModifiers();

        cachedLocalScale = transform.localScale;

        // Automatically spawn additional cubes if MultiCubeCount > 1
        if (MultiCubeCount > 1)
        {
            SpawnAdditionalCubes();
        }
    }

    private void Update()
    {
        // Smoothly update the actual audio volume toward the calculated target each frame
        if (currentSlideVolume != targetSlideVolume)
        {
            currentSlideVolume = Mathf.MoveTowards(currentSlideVolume, targetSlideVolume, audioFadeSpeed * Time.deltaTime);
        }

        if (slideAudioInstance != null)
        {
            slideAudioInstance.UpdateParameters(currentSlideVolume, currentPitchScale);
        }
    }

    #endregion

    #region Physics & Collision Handlers

    private void OnCollisionEnter(Collision collision)
    {
        collidedObjects++;

        // Only proceed if force exceeds threshold
        if (collision.impulse.magnitude / Time.fixedDeltaTime > minImpactForce)
        {
            OnImpact(collision);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collidedObjects == 0)
        {
            collidedObjects = 1;
        }

        // Determine intended target volume based on velocity and state
        if (rb.linearVelocity.magnitude <= MIN_SLIDE_VELOCITY)
        {
            targetSlideVolume = 0f;
        }
        else
        {
            targetSlideVolume = Mathf.Clamp01(rb.linearVelocity.magnitude / MAX_SLIDE_VELOCITY);

            // Apply direction-aligned surface acceleration if negative friction modifier is active
            if (negativeFrictionBoost > 0f && collision.contacts.Length > 0)
            {
                Vector3 surfaceNormal = collision.contacts[0].normal;
                Vector3 slideDirection = Vector3.ProjectOnPlane(rb.linearVelocity, surfaceNormal).normalized;

                // ForceMode.Acceleration scales automatically with mass for consistent feel
                rb.AddForce(slideDirection * negativeFrictionBoost, ForceMode.Acceleration);
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        collidedObjects = Mathf.Max(0, collidedObjects - 1);
        if (collidedObjects == 0)
        {
            targetSlideVolume = 0f;
        }
    }

    private void OnImpact(Collision collision)
    {
        if (Time.time < nextAllowedImpactTime) return;

        // Update debounce timestamp
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

        renderedCube.transform.DOPunchScale(
            punchStrength,
            duration: 0.35f,
            vibrato: 10,
            elasticity: 1f
        ).OnComplete(() => transform.localScale = cachedLocalScale);
    }

    private void PlayImpactParticles(Collision collision)
    {
        if (impactParticles == null || collision.contacts.Length == 0) return;

        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;

        // Only proceed if force exceeds high-impact threshold
        if (impactForce < minImpactForceForBink) return;

        // Get the primary contact point and position particle system at impact point
        ContactPoint contact = collision.contacts[0];
        impactParticles.transform.position = contact.point;

        impactParticles.Play();
    }

    private void PlayImpactSounds(Collision collision)
    {
        // Calculate impact force magnitude from impulse (scaled by fixed DeltaTime)
        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
        AudioCueSO targetCue = (impactForce > minImpactForceForBink) ? activeBinkSound : activeBonkSound;

        if (targetCue != null)
        {
            // Instantiate a transient runtime copy to avoid modifying the asset on disk
            AudioCueSO tempCue = ScriptableObject.Instantiate(targetCue);
            tempCue.basePitch = targetCue.GetRandomPitch(currentPitchScale);

            AudioManager.Instance.Play3DSFX(tempCue, transform.position);
        }
    }

    #endregion

    #region Modifier API & Logic

    public void AddModifier(CubeModifier modifier)
    {
        if (modifier == null) return;

        // Enforce rule: Only one CubeMaterialModifer allowed at a time
        if (modifier is CubeMaterialModifer)
        {
            modifiers.RemoveAll(m => m is CubeMaterialModifer);
        }

        modifiers.Add(modifier);
        ReapplyModifiers();
    }

    public void RemoveModifier(CubeModifier modifier)
    {
        if (modifier == null) return;

        if (modifiers.Remove(modifier))
        {
            ReapplyModifiers();
        }
    }

    public void ClearModifiers()
    {
        modifiers.Clear();
        ReapplyModifiers();
    }

    /// <summary>
    /// Spawns additional copies of this cube based on the current 'multi' modifier level.
    /// Newly spawned cubes have their 'multi' modifier level zeroed out to prevent recursive spawning.
    /// </summary>
    public List<Cube> SpawnAdditionalCubes()
    {
        List<Cube> spawnedCubes = new List<Cube>();
        int extraCubesToSpawn = multiCubeCount - 1;

        // Locate active Target Group in scene
        CinemachineTargetGroup targetGroup = FindFirstObjectByType<CinemachineTargetGroup>();

        // Calculate half-extent (radius) of the current cube's scale
        float minRadius = transform.localScale.x * 0.5f * Mathf.Sqrt(3f); // Distance to cube corner
        float maxRadius = transform.localScale.x * 4f;

        for (int i = 0; i < extraCubesToSpawn; i++)
        {
            // Get a random direction in the upper hemisphere
            Vector3 direction = Random.insideUnitSphere;
            direction.y = Mathf.Abs(direction.y);

            // Ensure direction vector is normalized before applying distance
            if (direction == Vector3.zero) direction = Vector3.up;
            direction.Normalize();

            // Randomize distance clamped strictly outside the original cube's bounds
            float distance = Random.Range(minRadius, maxRadius);
            Vector3 offset = direction * distance;

            Cube newCube = Instantiate(this, transform.position + offset, Quaternion.identity);

            // Duplicate modifier list so child instances are distinct ScriptableObjects
            newCube.modifiers = new List<CubeModifier>();

            foreach (var mod in this.modifiers)
            {
                if (mod == null) continue;

                // Instantiate a unique runtime copy of the ScriptableObject
                CubeModifier modInstance = Instantiate(mod);

                // Zero out multi parameter so cloned cubes cannot trigger further spawns
                modInstance.multi = 0;

                newCube.modifiers.Add(modInstance);
            }

            newCube.ReapplyModifiers();
            spawnedCubes.Add(newCube);

            // Add newly spawned cube to Cinemachine Target Group
            if (targetGroup != null)
            {
                targetGroup.AddMember(newCube.transform, weight: 1f, radius: newCube.transform.localScale.x * 0.5f);
            }
        }

        return spawnedCubes;
    }

    private void ReapplyModifiers()
    {
        // 1. Reset visual effect instances
        foreach (var fx in activeEffectInstances)
        {
            if (fx != null) Destroy(fx);
        }
        activeEffectInstances.Clear();

        // 2. Reset Audio and Material defaults
        activeBonkSound = defaultBonkSound;
        activeBinkSound = defaultBinkSound;

        if (cubeRenderer != null && defaultMaterial != null)
        {
            cubeRenderer.material = defaultMaterial;
        }

        // 3. Accumulate level sums from all modifiers
        int totalWeightLevel = 0;
        int totalStaticFrictionLevel = 0;
        int totalDynamicFrictionLevel = 0;
        int totalBouncinessLevel = 0;
        int totalSizeLevel = 0;
        int totalMultiLevel = 0;

        CubeMaterialModifer activeMaterialModifier = null;

        foreach (var mod in modifiers)
        {
            if (mod == null) continue;

            totalWeightLevel += mod.weight;
            totalStaticFrictionLevel += mod.staticFriction;
            totalDynamicFrictionLevel += mod.dynamicFriction;
            totalBouncinessLevel += mod.bounciness;
            totalSizeLevel += mod.size;
            totalMultiLevel += mod.multi;

            // Handle Effect Modifier (Visual updates)
            if (mod is CubeEffectModifier effectMod && effectMod.visualEffectPrefab != null)
            {
                Transform parentTransform = renderedCube != null ? renderedCube.transform : transform;
                GameObject fxInstance = Instantiate(effectMod.visualEffectPrefab, parentTransform);
                activeEffectInstances.Add(fxInstance);
            }

            // Capture the single Material Modifier (last one in list takes precedence if multiple were present)
            if (mod is CubeMaterialModifer matMod)
            {
                activeMaterialModifier = matMod;
            }
        }

        // Clamp combined sums to valid bounds [-7, 7]
        totalWeightLevel = Mathf.Clamp(totalWeightLevel, -7, 7);
        totalStaticFrictionLevel = Mathf.Clamp(totalStaticFrictionLevel, -7, 7);
        totalDynamicFrictionLevel = Mathf.Clamp(totalDynamicFrictionLevel, -7, 7);
        totalBouncinessLevel = Mathf.Clamp(totalBouncinessLevel, -7, 7);
        totalSizeLevel = Mathf.Clamp(totalSizeLevel, -7, 7);
        totalMultiLevel = Mathf.Clamp(totalMultiLevel, -7, 7);

        // Handle negative dynamic friction level scaling
        if (totalDynamicFrictionLevel < 0)
        {
            // Scale boost magnitude proportionally with how negative the level is
            negativeFrictionBoost = Mathf.Abs(totalDynamicFrictionLevel) * 0.09f;
        }
        else
        {
            negativeFrictionBoost = 0f;
        }

        // 4. Apply Material & Audio overrides
        if (activeMaterialModifier != null)
        {
            if (cubeRenderer != null && activeMaterialModifier.material != null)
            {
                cubeRenderer.material = activeMaterialModifier.material;
            }
            if (activeMaterialModifier.bonkSound != null)
            {
                activeBonkSound = activeMaterialModifier.bonkSound;
            }
            if (activeMaterialModifier.binkSound != null)
            {
                activeBinkSound = activeMaterialModifier.binkSound;
            }
        }

        // 5. Calculate and apply parameters using reasonable bounded formulas

        // Multi: Level <= 0 results in 1 cube; Levels 1 to 7 correspond to 2 to 8 cubes (1 + totalMultiLevel)
        multiCubeCount = Mathf.Max(1, 1 + totalMultiLevel);

        // Mass: Scaled exponentially around base mass 1.0 (Level 0 = 1kg, Level -7 = ~0.05kg, Level +7 = ~17kg)
        if (rb != null)
        {
            rb.mass = Mathf.Clamp(BASE_MASS * Mathf.Pow(1.5f, totalWeightLevel), 0.05f, 15f);
        }

        // Size: Scale vector scaled around 1.0 (Level 0 = (1,1,1), Level -7 = ~0.28, Level +7 = ~3.58)
        transform.localScale = BASE_SCALE * Mathf.Pow(1.2f, totalSizeLevel);

        // Calculate pitch multiplier inverse to object scale (Larger size = lower pitch, Smaller size = higher pitch)
        currentPitchScale = Mathf.Pow(1.2f, -totalSizeLevel);

        // Physics Material (Friction and Bounciness)
        if (cubeCollider != null)
        {
            PhysicsMaterial physMat = new PhysicsMaterial("CubeDynamicPhysicsMat")
            {
                staticFriction = Mathf.Clamp(BASE_STATIC_FRICTION + (totalStaticFrictionLevel * 0.14f), 0f, 1.0f),
                dynamicFriction = Mathf.Clamp(BASE_DYNAMIC_FRICTION + (totalDynamicFrictionLevel * 0.14f), 0f, 1.0f),
                bounciness = Mathf.Clamp(BASE_BOUNCINESS + (totalBouncinessLevel * 0.1f), 0.01f, 0.99f),
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            cubeCollider.material = physMat;
        }
    }

    #endregion
}