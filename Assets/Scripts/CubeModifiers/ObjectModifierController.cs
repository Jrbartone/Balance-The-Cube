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

    // Scoring Aggregates Exposed for External Trackers
    public int TotalPointsPerSlideSecond { get; private set; }
    public int TotalPointsPerHeightUnit { get; private set; }
    public int TotalPointsPerStillnessSecond { get; private set; }
    public int TotalPointsPerAirSecond { get; private set; }
    public int TotalPointsPerImpact { get; private set; }

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
        List<CubeModifier> processedModifiers = FilterModifiers(modifiers);

        ResetStateAndVisuals();

        Transform parentTransform = renderTarget != null ? renderTarget.transform : transform;
        bool hasBothIceAndSlime = CheckForIceAndSlimeCombo(processedModifiers);

        if (hasBothIceAndSlime && IceSlimePrefab != null)
        {
            activeEffectInstances.Add(Instantiate(IceSlimePrefab, parentTransform));
        }

        ModifierLevels levels = ProcessModifiersList(
            processedModifiers, 
            parentTransform, 
            hasBothIceAndSlime, 
            out CubeMaterialModifer activeMaterialModifier
        );

        ApplyMaterialAndAudio(activeMaterialModifier);
        ApplyPhysicalTransformations(levels);

        PhysicsMaterial physMat = ApplyPhysicsMaterial(levels);
        UpdateJellyMaterialIfPresent(physMat);

        if (TryGetComponent<ObjectImpactAudioFeedback>(out var audioFeedback))
        {
            audioFeedback.UpdateAudioLoops();
        }
    }

    #region Reapply Modifiers Pipeline Helpers

    private struct ModifierLevels
    {
        public int Weight;
        public int StaticFriction;
        public int DynamicFriction;
        public int Bounciness;
        public int Size;
        public int Multi;

        public void ClampAll(int min = -7, int max = 7)
        {
            Weight = Mathf.Clamp(Weight, min, max);
            StaticFriction = Mathf.Clamp(StaticFriction, min, max);
            DynamicFriction = Mathf.Clamp(DynamicFriction, min, max);
            Bounciness = Mathf.Clamp(Bounciness, min, max);
            Size = Mathf.Clamp(Size, min, max);
            Multi = Mathf.Clamp(Multi, min, max);
        }
    }

    private List<CubeModifier> FilterModifiers(List<CubeModifier> inputModifiers)
    {
        List<CubeModifier> processed = new List<CubeModifier>();
        HashSet<CubeModifier> seenNonStackable = new HashSet<CubeModifier>();

        foreach (var mod in inputModifiers)
        {
            if (mod == null) continue;
            if (!mod.canStack && !seenNonStackable.Add(mod)) continue;

            processed.Add(mod);
        }

        return processed;
    }

    private void ResetStateAndVisuals()
    {
        if (renderMask != null) renderMask.SetActive(true);

        foreach (var fx in activeEffectInstances)
        {
            if (fx != null) Destroy(fx);
        }
        activeEffectInstances.Clear();

        transform.localScale = BASE_SCALE;
        if (rb != null) rb.mass = BASE_MASS;

        ActiveBonkSound = null;
        ActiveBinkSound = null;
        ActiveSlideSound = null;
        CustomImpactParticlePrefab = null;
        AreImpactParticlesDisabled = false;
        ActiveLoopingSoundEffects.Clear();

        // Reset Scoring Aggregates
        TotalPointsPerSlideSecond = 0;
        TotalPointsPerHeightUnit = 0;
        TotalPointsPerStillnessSecond = 0;
        TotalPointsPerAirSecond = 0;
        TotalPointsPerImpact = 0;

        if (targetRenderer != null && defaultMaterial != null)
        {
            targetRenderer.material = defaultMaterial;
        }
    }

    private bool CheckForIceAndSlimeCombo(List<CubeModifier> processedModifiers)
    {
        bool hasIce = false;
        bool hasSlime = false;

        foreach (var mod in processedModifiers)
        {
            string modName = mod.name.ToLower();
            if (modName.Contains("ice")) hasIce = true;
            if (modName.Contains("slime")) hasSlime = true;
        }

        return hasIce && hasSlime;
    }

    private ModifierLevels ProcessModifiersList(
        List<CubeModifier> processedModifiers, 
        Transform parentTransform, 
        bool hasBothIceAndSlime, 
        out CubeMaterialModifer activeMaterialModifier)
    {
        ModifierLevels levels = new ModifierLevels();
        activeMaterialModifier = null;

        int highestAudioPriority = int.MinValue;
        int highestParticlePriority = int.MinValue;

        foreach (var mod in processedModifiers)
        {
            if (mod.disableRenderMask && renderMask != null)
            {
                renderMask.SetActive(false);
            }

            // Base Properties
            levels.Weight += mod.weight;
            levels.StaticFriction += mod.staticFriction;
            levels.DynamicFriction += mod.dynamicFriction;
            levels.Bounciness += mod.bounciness;
            levels.Size += mod.size;
            levels.Multi += mod.multi;

            // Scoring Aggregates
            TotalPointsPerSlideSecond += mod.pointsPerSlideSecond;
            TotalPointsPerHeightUnit += mod.pointsPerHeightUnit;
            TotalPointsPerStillnessSecond += mod.pointsPerStillnessSecond;
            TotalPointsPerAirSecond += mod.pointsPerAirSecond;
            TotalPointsPerImpact += mod.pointsPerImpact;

            if (mod is CubeEffectModifier effectMod)
            {
                ProcessEffectModifier(effectMod, parentTransform, hasBothIceAndSlime, ref highestParticlePriority);
            }

            if (mod is CubeMaterialModifer matMod)
            {
                activeMaterialModifier = matMod;
            }

            ProcessAudioOverrides(mod, ref highestAudioPriority);
        }

        levels.ClampAll(-7, 7);
        return levels;
    }

    private void ProcessEffectModifier(
        CubeEffectModifier effectMod, 
        Transform parentTransform, 
        bool hasBothIceAndSlime, 
        ref int highestParticlePriority)
    {
        if (effectMod.visualEffectPrefab != null)
        {
            string modName = effectMod.name.ToLower();
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

            if (effectMod.impactParticlePrefab != null && 
                effectMod.impactParticlePrefab.TryGetComponent(out ParticleSystem ps))
            {
                CustomImpactParticlePrefab = ps;
            }
        }
    }

    private void ProcessAudioOverrides(CubeModifier mod, ref int highestAudioPriority)
    {
        bool hasAudioOverride = mod.bonkSoundOverride != null || mod.binkSoundOverride != null || mod.slideSoundOverride != null;
        if (hasAudioOverride && mod.soundOverridePriority >= highestAudioPriority)
        {
            highestAudioPriority = mod.soundOverridePriority;
            if (mod.bonkSoundOverride != null) ActiveBonkSound = mod.bonkSoundOverride;
            if (mod.binkSoundOverride != null) ActiveBinkSound = mod.binkSoundOverride;
            if (mod.slideSoundOverride != null) ActiveSlideSound = mod.slideSoundOverride;
        }
    }

    private void ApplyMaterialAndAudio(CubeMaterialModifer activeMaterialModifier)
    {
        if (activeMaterialModifier == null) return;

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

    private void ApplyPhysicalTransformations(ModifierLevels levels)
    {
        negativeFrictionBoost = (levels.DynamicFriction < 0) ? Mathf.Abs(levels.DynamicFriction) * 0.09f : 0f;
        multiCubeCount = Mathf.Max(1, 1 + levels.Multi);

        if (rb != null)
        {
            rb.mass = Mathf.Clamp(BASE_MASS * Mathf.Pow(1.5f, levels.Weight), 0.05f, 15f);
        }

        transform.localScale = BASE_SCALE * Mathf.Pow(1.2f, levels.Size);
        currentPitchScale = Mathf.Pow(1.2f, -levels.Size);
    }

    private PhysicsMaterial ApplyPhysicsMaterial(ModifierLevels levels)
    {
        if (targetCollider == null) return null;

        PhysicsMaterial physMat = new PhysicsMaterial("DynamicPhysicsMat")
        {
            staticFriction = Mathf.Clamp(BASE_STATIC_FRICTION + (levels.StaticFriction * 0.14f), 0f, 1.0f),
            dynamicFriction = Mathf.Clamp(BASE_DYNAMIC_FRICTION + (levels.DynamicFriction * 0.14f), 0f, 1.0f),
            bounciness = Mathf.Clamp(BASE_BOUNCINESS + (levels.Bounciness * 0.1f), 0.01f, 0.99f),
            frictionCombine = PhysicsMaterialCombine.Average,
            bounceCombine = PhysicsMaterialCombine.Maximum
        };

        targetCollider.material = physMat;
        return physMat;
    }

    #endregion

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

            var newModifierController = newObject.GetComponent<ObjectModifierController>();
            if (newModifierController != null)
            {
                newModifierController.isSpawnedClone = true;
                newModifierController.CleanUpClonedFX();
                newModifierController.ClearModifiers();

                foreach (var mod in this.modifiers)
                {
                    if (mod == null) continue;
                    CubeModifier modInstance = Instantiate(mod);
                    modInstance.multi = 0;
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