using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class ObjectImpactBroadcaster : MonoBehaviour
{
    public enum ImpactType { Bonk, Bink }

    [System.Serializable] public class ImpactEvent : UnityEvent<Collision, ImpactType> { }
    [System.Serializable] public class AudioStateEvent : UnityEvent<float> { }

    [Header("Impact FX Tuning")]
    [SerializeField] private float minImpactForce = 20f;
    [SerializeField] private float minImpactForceForBink = 175f;
     [HideInInspector]
    [SerializeField] private float debounceCooldown = 0.4f;

    [Header("Slide & Aerial Audio Smoothing")]
    [SerializeField] private float audioFadeSpeed = 5f;

    [HideInInspector]
    public ImpactEvent OnImpact = new ImpactEvent();      // Fires for Bonk or Bink
    [HideInInspector]
    public AudioStateEvent OnSlide = new AudioStateEvent(); // Sends slide volume (0 to 1)
    [HideInInspector]
    public AudioStateEvent OnAir = new AudioStateEvent();   // Sends air volume (0 to 1)

    private Rigidbody rb;
    private ObjectModifierController modifierController;

    private const float MAX_SLIDE_VELOCITY = 5f;
    private const float MIN_SLIDE_VELOCITY = 1.5f;

    private float nextAllowedImpactTime;
    private float currentSlideVolume;
    private float currentAerialVolume;

    public ObjectModifierController ModifierController
    {
        get
        {
            if (modifierController == null)
            {
                modifierController = GetComponent<ObjectModifierController>();
            }
            return modifierController;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _ = ModifierController; // Force cache during Awake
    }

    private void Update()
    {
        float velocityMag = rb != null ? rb.linearVelocity.magnitude : 0f;
        bool isColliding = CheckIfColliding();

        float targetSlideVolume = (isColliding && velocityMag > MIN_SLIDE_VELOCITY) ? Mathf.Clamp01(velocityMag / MAX_SLIDE_VELOCITY) : 0f;
        float targetAerialVolume = (!isColliding && velocityMag > MIN_SLIDE_VELOCITY) ? Mathf.Clamp01(velocityMag / MAX_SLIDE_VELOCITY) : 0f;

        currentSlideVolume = Mathf.MoveTowards(currentSlideVolume, targetSlideVolume, audioFadeSpeed * Time.deltaTime);
        currentAerialVolume = Mathf.MoveTowards(currentAerialVolume, targetAerialVolume, audioFadeSpeed * Time.deltaTime);

        OnSlide?.Invoke(currentSlideVolume);
        OnAir?.Invoke(currentAerialVolume);
    }

    private void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
        if (impactForce > minImpactForce)
        {
            HandleImpact(collision, impactForce);
        }
    }

    public void HandleImpact(Collision collision, float impactForce)
    {
        if (Time.time < nextAllowedImpactTime) return;

        nextAllowedImpactTime = Time.time + debounceCooldown;

        ImpactType impactType = (impactForce > minImpactForceForBink) ? ImpactType.Bink : ImpactType.Bonk;
        OnImpact?.Invoke(collision, impactType);
    }

    public bool CheckIfColliding()
    {
        float radius = transform.localScale.x;
        int platformLayerMask = 1 << LayerMask.NameToLayer("Platform");
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius, platformLayerMask, QueryTriggerInteraction.Ignore);
        return hitColliders.Length > 0;
    }
}