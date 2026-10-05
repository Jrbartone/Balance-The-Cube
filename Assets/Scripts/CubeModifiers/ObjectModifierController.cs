using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class ObjectModifierController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject renderTarget;
    [SerializeField] private GameObject renderMask;

    [Header("Camera Integration")]
    [Tooltip("If true, spawned object instances will automatically be added to any CinemachineTargetGroup in the scene.")]
    [SerializeField] private bool trackSpawnedObjectsWithCamera = true;

    [Header("Modifiers")]
    [SerializeField] private List<CubeModifier> modifiers = new List<CubeModifier>();

    [HideInInspector]
    public UnityEvent<PhysicsMaterial> OnPhysicsMaterialUpdated;

    // Reset Constants
    private float BASE_MASS = 1.0f;
    private const float BASE_STATIC_FRICTION = 0f;
    private const float BASE_DYNAMIC_FRICTION = 0f;
    private const float BASE_BOUNCINESS = 0.3f;
    private static readonly Vector3 BASE_SCALE = Vector3.one;

    // Component References
    private Rigidbody rb;
    private Renderer targetRenderer;
    private Collider targetCollider;
    private Material defaultMaterial;

    // Runtime State
    private readonly List<GameObject> activeEffectInstances = new List<GameObject>();
    private float negativeFrictionBoost;
    private int multiCubeCount = 1;
    private float currentPitchScale = 1.0f;
    private bool isSpawnedClone = false;

    // Resolved Audio/Particle Overrides
    public AudioCueSO ActiveBonkSound { get; private set; }
    public AudioCueSO ActiveBinkSound { get; private set; }
    public AudioCueSO ActiveSlideSound { get; private set; }
    public ParticleSystem CustomImpactParticlePrefab { get; private set; }
    public bool AreImpactParticlesDisabled { get; private set; }
    public List<AudioCueSO> ActiveLoopingSoundEffects { get; private set; } = new List<AudioCueSO>();

    public int MultiCubeCount => multiCubeCount;
    public float CurrentPitchScale => currentPitchScale;
    public List<CubeModifier> Modifiers => modifiers;
    public GameObject RenderMask => renderMask;
    public GameObject RenderTarget => renderTarget;

    private static ModifierConfigSO config;
    private GameObject IceSlimePrefab => config != null ? config.IceSlimePrefab : null;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        BASE_MASS = rb.mass;
        targetCollider = GetComponent<Collider>();
        if (config == null)
        {
            config = Resources.Load<ModifierConfigSO>("ModifierConfig");
        }

        if (renderTarget != null && renderTarget.TryGetComponent(out targetRenderer))
        {
            defaultMaterial = targetRenderer.sharedMaterial;
        }
    }

    private void Start()
    {
        ReapplyModifiers();

        // Handle auto-spawning if configured via multi modifier and this isn't already a clone
        if (!isSpawnedClone && multiCubeCount > 1)
        {
            SpawnAdditionalObjects();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (negativeFrictionBoost > 0f && rb != null && collision.contacts.Length > 0)
        {
            Vector3 surfaceNormal = collision.contacts[0].normal;
            Vector3 slideDirection = Vector3.ProjectOnPlane(rb.linearVelocity, surfaceNormal).normalized;
            rb.AddForce(slideDirection * negativeFrictionBoost, ForceMode.Acceleration);
        }
    }

    #region Modifier Management

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

    public void ReapplyModifiers()
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

        transform.localScale = BASE_SCALE;
        if (rb != null) rb.mass = BASE_MASS;

        // Reset defaults
        ActiveBonkSound = null;
        ActiveBinkSound = null;
        ActiveSlideSound = null;
        CustomImpactParticlePrefab = null;
        AreImpactParticlesDisabled = false;
        ActiveLoopingSoundEffects.Clear();

        int highestAudioPriority = int.MinValue;
        int highestParticlePriority = int.MinValue;

        if (targetRenderer != null && defaultMaterial != null)
        {
            targetRenderer.material = defaultMaterial;
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
        Transform parentTransform = renderTarget != null ? renderTarget.transform : transform;

        if (hasBothIceAndSlime && IceSlimePrefab != null)
        {
            activeEffectInstances.Add(Instantiate(IceSlimePrefab, parentTransform));
        }

        int totalWeightLevel = 0, totalStaticFrictionLevel = 0, totalDynamicFrictionLevel = 0;
        int totalBouncinessLevel = 0, totalSizeLevel = 0, totalMultiLevel = 0;

        CubeMaterialModifer activeMaterialModifier = null;

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
                    ActiveLoopingSoundEffects.Add(effectMod.loopingSoundEffect);
                }

                if (effectMod.impactParticlePriority >= highestParticlePriority)
                {
                    highestParticlePriority = effectMod.impactParticlePriority;
                    AreImpactParticlesDisabled = effectMod.disableImpactParticles;

                    if (effectMod.impactParticlePrefab != null)
                    {
                        if (effectMod.impactParticlePrefab.TryGetComponent(out ParticleSystem ps))
                        {
                            CustomImpactParticlePrefab = ps;
                        }
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
                if (mod.bonkSoundOverride != null) ActiveBonkSound = mod.bonkSoundOverride;
                if (mod.binkSoundOverride != null) ActiveBinkSound = mod.binkSoundOverride;
                if (mod.slideSoundOverride != null) ActiveSlideSound = mod.slideSoundOverride;
            }
        }

        // Clamp levels to [-7, 7]
        totalWeightLevel = Mathf.Clamp(totalWeightLevel, -7, 7);
        totalStaticFrictionLevel = Mathf.Clamp(totalStaticFrictionLevel, -7, 7);
        totalDynamicFrictionLevel = Mathf.Clamp(totalDynamicFrictionLevel, -7, 7);
        totalBouncinessLevel = Mathf.Clamp(totalBouncinessLevel, -7, 7);
        totalSizeLevel = Mathf.Clamp(totalSizeLevel, -7, 7);
        totalMultiLevel = Mathf.Clamp(totalMultiLevel, -7, 7);

        negativeFrictionBoost = (totalDynamicFrictionLevel < 0) ? Mathf.Abs(totalDynamicFrictionLevel) * 0.09f : 0f;

        if (activeMaterialModifier != null)
        {
            if (targetRenderer != null && activeMaterialModifier.material != null)
            {
                targetRenderer.material = activeMaterialModifier.material;
            }

            if (ActiveBonkSound == null && activeMaterialModifier.bonkSound != null)
            {
                ActiveBonkSound = activeMaterialModifier.bonkSound;
            }
            if (ActiveBinkSound == null && activeMaterialModifier.binkSound != null)
            {
                ActiveBinkSound = activeMaterialModifier.binkSound;
            }
        }

        multiCubeCount = Mathf.Max(1, 1 + totalMultiLevel);

        if (rb != null)
        {
            rb.mass = Mathf.Clamp(BASE_MASS * Mathf.Pow(1.5f, totalWeightLevel), 0.05f, 15f);
        }

        transform.localScale = BASE_SCALE * Mathf.Pow(1.2f, totalSizeLevel);
        currentPitchScale = Mathf.Pow(1.2f, -totalSizeLevel);

        PhysicsMaterial physMat = null;
        if (targetCollider != null)
        {
            physMat = new PhysicsMaterial("DynamicPhysicsMat")
            {
                staticFriction = Mathf.Clamp(BASE_STATIC_FRICTION + (totalStaticFrictionLevel * 0.14f), 0f, 1.0f),
                dynamicFriction = Mathf.Clamp(BASE_DYNAMIC_FRICTION + (totalDynamicFrictionLevel * 0.14f), 0f, 1.0f),
                bounciness = Mathf.Clamp(BASE_BOUNCINESS + (totalBouncinessLevel * 0.1f), 0.01f, 0.99f),
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            targetCollider.material = physMat;
        }

        UpdateJellyMaterialIfPresent(physMat);
        OnPhysicsMaterialUpdated?.Invoke(physMat);
    }

    public List<GameObject> SpawnAdditionalObjects()
    {
        List<GameObject> spawnedObjects = new List<GameObject>();
        int extraObjectsToSpawn = multiCubeCount - 1;

        if (extraObjectsToSpawn <= 0) return spawnedObjects;

        CinemachineTargetGroup targetGroup = trackSpawnedObjectsWithCamera ? FindFirstObjectByType<CinemachineTargetGroup>() : null;

        float minRadius = BASE_SCALE.x * 0.5f * Mathf.Sqrt(3f);
        float maxRadius = BASE_SCALE.x * 4f;

        for (int i = 0; i < extraObjectsToSpawn; i++)
        {
            Vector3 direction = Random.insideUnitSphere;
            direction.y = Mathf.Abs(direction.y);
            if (direction == Vector3.zero) direction = Vector3.up;
            direction.Normalize();

            Vector3 offset = direction * Random.Range(minRadius, maxRadius);
            GameObject newObject = Instantiate(gameObject, transform.position + offset, Quaternion.identity);

            // Configure spawned object's modifier controller
            var newModifierController = newObject.GetComponent<ObjectModifierController>();
            if (newModifierController != null)
            {
                newModifierController.isSpawnedClone = true; // Prevents recursive spawning loop
                newModifierController.CleanUpClonedFX();
                newModifierController.ClearModifiers();

                foreach (var mod in this.modifiers)
                {
                    if (mod == null) continue;
                    CubeModifier modInstance = Instantiate(mod);
                    modInstance.multi = 0; // Reset multi count on clones
                    newModifierController.AddModifier(modInstance);
                }
            }

            var newImpactFeedback = newObject.GetComponent<ObjectImpactVisualFeedback>();
            if (newImpactFeedback != null)
            {
                newImpactFeedback.CleanUpClonedParticles();
            }

            spawnedObjects.Add(newObject);

            if (targetGroup != null)
            {
                targetGroup.AddMember(newObject.transform, weight: 1f, radius: newObject.transform.localScale.x * 0.5f);
            }
        }

        return spawnedObjects;
    }

    public void UpdateJellyMaterialIfPresent(PhysicsMaterial physicsMat = null)
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

    public void CleanUpClonedFX()
    {
        foreach (var fx in activeEffectInstances)
        {
            if (fx != null) Destroy(fx);
        }
        activeEffectInstances.Clear();

        Transform parentTransform = renderTarget != null ? renderTarget.transform : transform;
        for (int i = parentTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = parentTransform.GetChild(i);
            if (child.gameObject == renderMask) continue;
            Destroy(child.gameObject);
        }
    }

    #endregion
}