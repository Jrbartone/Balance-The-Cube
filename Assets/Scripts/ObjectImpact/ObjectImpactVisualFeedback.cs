using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
[RequireComponent(typeof(ObjectImpactBroadcaster))]
public class ObjectImpactVisualFeedback : MonoBehaviour
{
    [SerializeField] private GameObject targetToPunch;

    [Header("Particles")]
    [SerializeField] private ParticleSystem defaultImpactParticles;
    private ParticleSystem spawnedCustomImpactParticles;
    private ParticleSystem lastCustomPrefabReference;

    private ObjectImpactBroadcaster broadcaster;
    private ObjectModifierController modifierController;

    private void Awake()
    {
        broadcaster = GetComponent<ObjectImpactBroadcaster>();
        modifierController = broadcaster.ModifierController;
    }

    private void OnEnable() => broadcaster.OnImpact.AddListener(OnImpactHandled);
    private void OnDisable() => broadcaster.OnImpact.RemoveListener(OnImpactHandled);

    private void OnImpactHandled(Collision collision, ObjectImpactBroadcaster.ImpactType impactType)
    {
        PlayImpactParticles(collision, impactType);
    }

    private void PlayImpactTween(Collision collision, ObjectImpactBroadcaster.ImpactType impactType)
    {
        if (targetToPunch == null) return;

        float randomMagnitude = (impactType == ObjectImpactBroadcaster.ImpactType.Bonk) ? 0.1f : 0.3f;

        targetToPunch.transform.DOKill(true);
        targetToPunch.transform.localScale = Vector3.one;

        Vector3 punchStrength = (Vector3.one + Random.insideUnitSphere * randomMagnitude)
            * Mathf.Clamp(collision.relativeVelocity.magnitude * 0.05f, 0.05f, 0.3f);

        GameObject renderMask = modifierController != null ? modifierController.RenderMask : null;
        bool canPunch = renderMask == null || renderMask.activeSelf;

        if (canPunch)
        {
            Vector3 cachedScale = transform.localScale;
            targetToPunch.transform.DOPunchScale(punchStrength, duration: 0.35f, vibrato: 10, elasticity: 1f)
                .OnComplete(() => transform.localScale = cachedScale);
        }
    }

    private void PlayImpactParticles(Collision collision, ObjectImpactBroadcaster.ImpactType impactType)
    {
        if (modifierController != null && modifierController.AreImpactParticlesDisabled) return;
        if (collision.contacts.Length == 0 || impactType != ObjectImpactBroadcaster.ImpactType.Bink) return;

        ParticleSystem targetParticle = defaultImpactParticles;

        if (modifierController != null && modifierController.CustomImpactParticlePrefab != null)
        {
            ParticleSystem customPrefab = modifierController.CustomImpactParticlePrefab;
            if (spawnedCustomImpactParticles == null || lastCustomPrefabReference != customPrefab)
            {
                if (spawnedCustomImpactParticles != null) Destroy(spawnedCustomImpactParticles.gameObject);
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