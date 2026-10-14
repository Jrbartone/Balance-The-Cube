using UnityEngine;

public class FollowGameObject : MonoBehaviour
{
    [Header("Emulated Parent Configuration")]
    [Tooltip("The transform to follow as a virtual parent.")]
    public Transform targetParent;

    [Header("Follow Settings")]
    [Tooltip("If true, follows the target parent's position.")]
    public bool followPosition = true;

    [Tooltip("If true, follows the target parent's rotation.")]
    public bool followRotation = true;

    [Tooltip("If true, retains current relative offset when assigned. If false, snaps to parent's origin.")]
    public bool maintainOffset = true;

    [Tooltip("Update mode to match parent motion smoothly.")]
    public UpdateMode updateMode = UpdateMode.LateUpdate;

    public enum UpdateMode
    {
        Update,
        FixedUpdate,
        LateUpdate
    }

    private Vector3 localPositionOffset;
    private Quaternion localRotationOffset;

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

        if (targetParent == null) return;

        if (maintainOffset)
        {
            // Calculate initial local offsets relative to target parent's space
            localPositionOffset = targetParent.InverseTransformPoint(transform.position);
            localRotationOffset = Quaternion.Inverse(targetParent.rotation) * transform.rotation;
        }
        else
        {
            // Snap to exact parent location/rotation
            localPositionOffset = Vector3.zero;
            localRotationOffset = Quaternion.identity;
        }
    }

    private void Update()
    {
        if (updateMode == UpdateMode.Update)
        {
            FollowParent();
        }
    }

    private void FixedUpdate()
    {
        if (updateMode == UpdateMode.FixedUpdate)
        {
            FollowParent();
        }
    }

    private void LateUpdate()
    {
        if (updateMode == UpdateMode.LateUpdate)
        {
            FollowParent();
        }
    }

    private void FollowParent()
    {
        if (targetParent == null) return;

        // Apply position tracking independently if enabled
        if (followPosition)
        {
            transform.position = targetParent.TransformPoint(localPositionOffset);
        }

        // Apply rotation tracking independently if enabled
        if (followRotation)
        {
            transform.rotation = targetParent.rotation * localRotationOffset;
        }
    }
}