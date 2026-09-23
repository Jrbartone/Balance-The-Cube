using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FollowGameObjectRigidbody : MonoBehaviour
{
    [Header("Emulated Parent Configuration")]
    [Tooltip("The transform to follow as a virtual parent.")]
    public Transform targetParent;

    [Tooltip("If true, retains current relative offset when assigned. If false, snaps to parent's origin.")]
    public bool maintainOffset = true;

    [Tooltip("If checked, target's Rigidbody (if present) will be used to pass exact velocity. Otherwise, velocity is calculated delta-based.")]
    public bool useTargetRigidbodyVelocities = true;

    private Rigidbody rb;
    private Rigidbody parentRb;

    private Vector3 localPositionOffset;
    private Quaternion localRotationOffset;

    private Vector3 lastParentPosition;
    private Quaternion lastParentRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (targetParent != null)
        {
            SetTargetParent(targetParent, maintainOffset);
        }
    }

    /// <summary>
    /// Sets or changes the target parent dynamically at runtime.
    /// </summary>
    public void SetTargetParent(Transform newParent, bool keepOffset = true)
    {
        targetParent = newParent;
        maintainOffset = keepOffset;

        if (targetParent == null)
        {
            parentRb = null;
            return;
        }

        // Cache parent Rigidbody if available
        parentRb = targetParent.GetComponent<Rigidbody>();

        // Ensure non-kinematic body so velocities take effect
        rb.isKinematic = false;

        if (maintainOffset)
        {
            localPositionOffset = targetParent.InverseTransformPoint(rb.position);
            localRotationOffset = Quaternion.Inverse(targetParent.rotation) * rb.rotation;
        }
        else
        {
            localPositionOffset = Vector3.zero;
            localRotationOffset = Quaternion.identity;
        }

        lastParentPosition = targetParent.position;
        lastParentRotation = targetParent.rotation;
    }

    private void FixedUpdate()
    {
        FollowParentAndMatchVelocity();
    }

    private void FollowParentAndMatchVelocity()
    {
        if (targetParent == null) return;

        // 1. Calculate and move to updated position/rotation
        Vector3 targetWorldPosition = targetParent.TransformPoint(localPositionOffset);
        Quaternion targetWorldRotation = targetParent.rotation * localRotationOffset;

        rb.MovePosition(targetWorldPosition);
        rb.MoveRotation(targetWorldRotation);

        // 2. Transfer Velocity
        Vector3 parentLinearVel;
        Vector3 parentAngularVel;

        if (useTargetRigidbodyVelocities && parentRb != null)
        {
            // Unity 6+: Use parentRb.linearVelocity
            // Pre-Unity 6: Change parentRb.linearVelocity -> parentRb.velocity
            parentLinearVel = parentRb.linearVelocity;
            parentAngularVel = parentRb.angularVelocity;

            // Include tangential velocity from parent rotational speed if offset exists
            if (localPositionOffset != Vector3.zero)
            {
                Vector3 offsetVector = targetWorldPosition - targetParent.position;
                parentLinearVel += Vector3.Cross(parentAngularVel, offsetVector);
            }
        }
        else
        {
            // Fallback delta calculation if parent has no Rigidbody or option disabled
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            parentLinearVel = (targetParent.position - lastParentPosition) / dt;

            // Calculate angular velocity from rotation delta
            Quaternion rotationDelta = targetParent.rotation * Quaternion.Inverse(lastParentRotation);
            rotationDelta.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);

            // Normalize angle within [-180, 180] degrees
            if (angleInDegrees > 180f) angleInDegrees -= 360f;

            parentAngularVel = rotationAxis * (angleInDegrees * Mathf.Deg2Rad / dt);

            // Cache for next frame delta calculation
            lastParentPosition = targetParent.position;
            lastParentRotation = targetParent.rotation;
        }

        // Apply calculated velocities to this object's Rigidbody
        // Unity 6+: Use rb.linearVelocity
        // Pre-Unity 6: Change rb.linearVelocity -> rb.velocity
        rb.linearVelocity = parentLinearVel;
        rb.angularVelocity = parentAngularVel;
    }
}