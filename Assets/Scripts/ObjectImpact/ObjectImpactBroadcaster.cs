using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class ObjectImpactBroadcaster : MonoBehaviour
{
    public enum ImpactType { Bonk, Bink }
    public enum MovementState
    {
        None,
        Slide,
        Air,
        Still
    }

    [System.Serializable] public class ImpactEvent : UnityEvent<Collision, ImpactType> { }
    [System.Serializable] public class AudioStateEvent : UnityEvent<float> { }
    [System.Serializable] public class ApexEvent : UnityEvent { }
    [System.Serializable] public class StateTransitionEvent : UnityEvent<MovementState, MovementState, float> { } 
    [System.Serializable] public class StateTickEvent : UnityEvent<MovementState, float, float> { } 

    [Header("Impact FX Tuning")]
    [SerializeField] private float minImpactForce = 20f;
    [SerializeField] private float minImpactForceForBink = 175f;
    [HideInInspector]
    [SerializeField] private float debounceCooldown = 0.4f;

    [Header("Slide & Aerial Audio Smoothing")]
    [SerializeField] private float audioFadeSpeed = 5f;

    [HideInInspector] public ImpactEvent OnImpact = new ImpactEvent();
    [HideInInspector] public ApexEvent OnApex = new ApexEvent();
    [HideInInspector] public StateTransitionEvent OnStateTransition = new StateTransitionEvent();
    [HideInInspector] public StateTickEvent OnStateTick = new StateTickEvent();

    private Rigidbody rb;
    private ObjectModifierController modifierController;

    private const float MAX_SLIDE_VELOCITY = 5f;
    private const float MIN_SLIDE_VELOCITY = 0.5f;
    private const float MAX_SLIDE_VELOCITY_Y = 2f;
    private const float MAX_STILLNESS_ANGULAR_VELOCITY = 0.1f;

    private float nextAllowedImpactTime;
    private float currentSlideVolume;
    private float currentAerialVolume;
    private float previousYVelocity; 
    private bool wasColliding;

    private MovementState currentState = MovementState.None;
    private float timeInState;

    public MovementState CurrentState => currentState;
    public float TimeInState => timeInState;

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
        _ = ModifierController;
        wasColliding = CheckIfColliding();
    }

    private void Update()
    {
        float velocityMag = rb != null ? rb.linearVelocity.magnitude : 0f;
        Vector3 horizontalVelocity = rb != null ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z) : Vector3.zero;
        Vector3 verticalVelocity = rb != null ? new Vector3(0f, rb.linearVelocity.y, 0f) : Vector3.zero;
        float horizontalVelocityMag = horizontalVelocity.magnitude;
        float verticalVelocityMag = verticalVelocity.magnitude;
        float currentYVelocity = rb != null ? rb.linearVelocity.y : 0f;

        bool isColliding = CheckIfColliding();

        if (isColliding && !wasColliding)
        {
            currentAerialVolume = 0f;
            maybeUpdateState(MovementState.None);
        } 
        else if (!isColliding && wasColliding)
        {
        }

        if (!isColliding && previousYVelocity > 0f && currentYVelocity <= 0f && IsMoreThanOneMeterOffGround())
        {
            OnApex?.Invoke();
        }
        previousYVelocity = currentYVelocity;

        float targetSlideVolume = (isColliding && horizontalVelocityMag > MIN_SLIDE_VELOCITY) 
            ? Mathf.Clamp01(horizontalVelocityMag / MAX_SLIDE_VELOCITY) 
            : 0f;

        float targetAerialVolume = (!isColliding && velocityMag > MIN_SLIDE_VELOCITY) 
            ? Mathf.Clamp01(velocityMag / MAX_SLIDE_VELOCITY) 
            : 0f;

        currentSlideVolume = Mathf.MoveTowards(currentSlideVolume, targetSlideVolume, audioFadeSpeed * Time.deltaTime);
        currentAerialVolume = Mathf.MoveTowards(currentAerialVolume, targetAerialVolume, audioFadeSpeed * Time.deltaTime);

        MovementState targetState = MovementState.None;

        if (isColliding && horizontalVelocityMag >= MIN_SLIDE_VELOCITY && verticalVelocityMag <= MAX_SLIDE_VELOCITY_Y)
        {
            targetState = MovementState.Slide;
            timeInState += Time.fixedDeltaTime * ModifierController.TotalPointsPerSlideSecond;
        }
        else if (isColliding && rb.linearVelocity.magnitude < 0.5f && verticalVelocityMag <= MAX_SLIDE_VELOCITY_Y && rb.angularVelocity.magnitude <= MAX_STILLNESS_ANGULAR_VELOCITY)
        {
            targetState = MovementState.Still;
            timeInState += Time.fixedDeltaTime * ModifierController.TotalPointsPerStillnessSecond;
        }
        else if (!isColliding)
        {
            targetState = MovementState.Air;
            timeInState += Time.fixedDeltaTime * ModifierController.TotalPointsPerAirSecond;
        }
        else
        {
            targetState = MovementState.None;
        }


        maybeUpdateState(targetState);
        wasColliding = isColliding;
    }

    private void maybeUpdateState(MovementState newState)
    {
        if (currentState != newState)
        {
            MovementState oldState = currentState;
            float finalTime = timeInState;
            currentState = newState;
            timeInState = 0f;
            OnStateTransition?.Invoke(oldState, newState, finalTime);
        }  else {
            if (currentState != MovementState.None)
            {
                OnStateTick?.Invoke(currentState, timeInState, getCurrentStateVolume());
            }
        }
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

    private bool IsMoreThanOneMeterOffGround()
    {
        int platformLayerMask = 1 << LayerMask.NameToLayer("Platform");
        return !Physics.Raycast(transform.position, Vector3.down, 1.0f, platformLayerMask, QueryTriggerInteraction.Ignore);
    }

    private float getCurrentStateVolume()
    {
        switch (currentState)
        {
            case MovementState.Slide:
                return currentSlideVolume;
            case MovementState.Air:
                return currentAerialVolume;
            default:
                return 0f;
        }
    }
}