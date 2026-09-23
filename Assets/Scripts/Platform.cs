using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine; // Use Cinemachine if using older package versions (v2)
using DG.Tweening;

public class Platform : MonoBehaviour
{
    [Header("Camera Control")]
    [Tooltip("Reference to the Cinemachine Target Group focusing on this platform")]
    [SerializeField] private CinemachineTargetGroup targetGroup;
    private InputAction moveActionMouse;
    private InputAction moveActionLeftStick;
    private InputAction moveActionRightStick;
    private InputAction clickAction;
    private InputAction activateRightHand;
    private InputAction activateLeftHand;
    private Vector2 inputVector;
    private Rigidbody rb;
    bool dropped = false;
    public GameObject visiblePlatform;
    public GameObject[] handPositionMarkers;
    public GameObject handThree;

    [Header("Angle Limits")]
    [Tooltip("Maximum front/back tilt in degrees (Y input)")]
    [SerializeField, Range(0f, 89f)] private float maxPitchAngle = 30f;

    [Tooltip("Maximum side/side tilt in degrees (X input)")]
    [SerializeField, Range(0f, 89f)] private float maxRollAngle = 30f;

    [Header("Smoothing")]
    [Tooltip("Interpolation speed for smoothing the tilt")]
    [SerializeField] private float lerpSpeed = 1f;

    [Header("Kinematic Impact Response")]
    [Tooltip("Scales the incoming impact force into rotational offset")]
    [SerializeField] private float impactSensitivity = 0.05f;

    [Tooltip("Scales sustained resting mass into continuous rotational offset")]
    [SerializeField] private float weightSensitivity = 0.02f;

    [Tooltip("Speed at which the impact rotation springs back to zero")]
    [SerializeField] private float impactRecoverySpeed = 8f;

    [Header("Vertical Bounce")]
    [Tooltip("Scales impact forces into vertical Y displacement")]
    [SerializeField] private float verticalImpactSensitivity = 0.015f;

    [Tooltip("Speed at which the Y position springs back to resting height")]
    [SerializeField] private float verticalRecoverySpeed = 12f;

    // Stores current rotational impact offset (Euler pitch, yaw, roll)
    private Vector3 currentImpactOffset;
    // Sustained offset target calculated per frame from resting objects
    private Vector3 targetWeightOffset;
    private Vector3 currentWeightOffset;
    // Base resting position
    private Vector3 baseLocalPosition;
    // Current vertical displacement offset
    private float currentYOffset;

    private bool isRightHandActive = false;
    private bool isLeftHandActive = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    
    void Start()
    {
        moveActionMouse = InputSystem.actions.FindAction("MoveMouse");
        moveActionLeftStick = InputSystem.actions.FindAction("MoveLeftStick");
        moveActionRightStick = InputSystem.actions.FindAction("MoveRightStick");
        clickAction = InputSystem.actions.FindAction("Click");
        activateLeftHand = InputSystem.actions.FindAction("ActivateLeftHand"); 
        activateRightHand = InputSystem.actions.FindAction("ActivateRightHand");
        rb = GetComponent<Rigidbody>();
        // Cache initial local position for Y bounce calculations
        baseLocalPosition = transform.localPosition;
    }

    void Update()
    {
        handleInput();
    }

    void FixedUpdate(){
       handlePositionAndRotation();
       // Reset target weight accumulator for the next frame's OnCollisionStay calls
        targetWeightOffset = Vector3.zero;
    }

    void handleInput(){
        isLeftHandActive = activateLeftHand.ReadValue<float>() != 0;
        isRightHandActive = activateRightHand.ReadValue<float>() != 0;
        if((isLeftHandActive && isRightHandActive) || dropped){
            inputVector = Vector2.zero;
            return;
        }
        if(DeviceDetector.Instance.IsGamepad){
            if(isLeftHandActive){
                inputVector = moveActionRightStick.ReadValue<Vector2>();
            } else if(isRightHandActive){
                inputVector = moveActionLeftStick.ReadValue<Vector2>();
            } else {
                Vector2 leftInput = moveActionLeftStick.ReadValue<Vector2>();
                Vector2 rightInput = moveActionRightStick.ReadValue<Vector2>();
                inputVector = (leftInput.sqrMagnitude > rightInput.sqrMagnitude) ? leftInput : rightInput;
            }
        } else {
            if(isMouseHoldingDown()){
                inputVector = (inputVector+moveActionMouse.ReadValue<Vector2>());
            } else {
                inputVector = Vector2.zero;
            }
        }
    }

    void handlePositionAndRotation()
    {
        if(!handThree.activeSelf) {
            if((isLeftHandActive && isRightHandActive) || dropped){
                if (!dropped)
                {
                    DropPlatform();
                }
                return;
            }
        }
        // --- 1. Position Bounce Handling ---
        // Smoothly return vertical Y offset to zero
        currentYOffset = Mathf.Lerp(currentYOffset, 0f, Time.fixedDeltaTime * verticalRecoverySpeed);

        Vector3 targetLocalPosition = baseLocalPosition + new Vector3(0f, currentYOffset, 0f);
        Vector3 targetWorldPosition = (transform.parent != null)
            ? transform.parent.TransformPoint(targetLocalPosition)
            : targetLocalPosition;

        rb.MovePosition(targetWorldPosition);

        // --- 2. Rotation Tilt Handling ---
        float clampedY = Mathf.Clamp(inputVector.y, -1f, 1f);
        float clampedX = Mathf.Clamp(inputVector.x, -1f, 1f);

        float targetPitch = clampedY * maxPitchAngle;
        float targetRoll = -clampedX * maxRollAngle;

        currentImpactOffset = Vector3.Lerp(currentImpactOffset, Vector3.zero, Time.fixedDeltaTime * impactRecoverySpeed);
        currentWeightOffset = Vector3.Lerp(currentWeightOffset, targetWeightOffset, Time.fixedDeltaTime * lerpSpeed);

        Quaternion combinedOffset = Quaternion.Euler(currentImpactOffset + currentWeightOffset);
        Quaternion targetLocalRotation = Quaternion.Euler(targetPitch, 0f, targetRoll) * combinedOffset;

        Quaternion targetWorldRotation = (transform.parent != null) 
            ? transform.parent.rotation * targetLocalRotation 
            : targetLocalRotation;

        Quaternion nextRotation = Quaternion.Slerp(
            rb.rotation,
            targetWorldRotation,
            Time.fixedDeltaTime * lerpSpeed
        );

        rb.MoveRotation(nextRotation);
    }

    public void AddImpactAtPoint(Vector3 point, Vector3 impulseForce)
    {
        Vector3 leverArm = point - transform.position;
        Vector3 worldTorque = Vector3.Cross(leverArm, impulseForce);
        Vector3 localTorque = transform.InverseTransformDirection(worldTorque);

        // Extract downward component of the impact relative to the platform surface
        float verticalForce = Vector3.Dot(-impulseForce, transform.up);

        if (visiblePlatform != null)
        {
            Transform targetTransform = visiblePlatform.transform;

            // Complete and clear active tweens so local position/rotation reset to baseline before starting new punch
            targetTransform.DOComplete();

            // Calculate punch vectors in local coordinate space
            Vector3 localPositionPunch = new Vector3(0f, -Mathf.Abs(verticalForce) * verticalImpactSensitivity, 0f);
            Vector3 localRotationPunch = localTorque * impactSensitivity * 10f;

            // DOPunchPosition with optional bool isLocal = true
            targetTransform.DOPunchPosition(localPositionPunch, duration: 0.3f, vibrato: 8, elasticity: 0.5f, snapping: false);

            // DOPunchRotation applies directly as local Euler angles relative to local rotation
            targetTransform.DOPunchRotation(localRotationPunch, duration: 0.3f, vibrato: 8, elasticity: 0.5f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("JellyCube"))
        {
            return;
        }
        Rigidbody otherRb = collision.rigidbody;
        float mass = (otherRb != null && otherRb.mass < 2f) ? otherRb.mass : 2f;
        Vector3 impulse = collision.relativeVelocity * mass;

        foreach (ContactPoint contact in collision.contacts)
        {
           AddImpactAtPoint(contact.point, -impulse / collision.contactCount);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("JellyCube"))
        {
            return;
        }
        Rigidbody otherRb = collision.rigidbody;
        float mass = (otherRb != null) ? otherRb.mass : 1f;

        Vector3 gravityForce = Physics.gravity * mass;

        foreach (ContactPoint contact in collision.contacts)
        {
            Vector3 leverArm = contact.point - transform.position;
            Vector3 worldTorque = Vector3.Cross(leverArm, gravityForce);
            Vector3 localTorque = transform.InverseTransformDirection(worldTorque);

            targetWeightOffset += (localTorque * weightSensitivity) / collision.contactCount;
        }
    }

    bool isMouseHoldingDown()
    {
        return clickAction.ReadValue<float>() != 0;
    }

    private void DropPlatform()
    {
        rb.isKinematic = false;
        rb.useGravity = true;
        dropped = true;
        foreach(GameObject g in handPositionMarkers){
            g.transform.parent = null;
        }
        RemoveFromTargetGroup();
    }

    private void RemoveFromTargetGroup()
    {
        if (targetGroup != null)
        {
            targetGroup.RemoveMember(transform);
        }
    }
}
