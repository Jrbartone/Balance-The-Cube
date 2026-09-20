using UnityEngine;
using Unity.Cinemachine;

public class RemoveFromCameraTrackingZone : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The Cinemachine Target Group to remove objects from.")]
    [SerializeField] private CinemachineTargetGroup targetGroup;

    [Tooltip("Tag required on entering objects (leave empty to allow any object).")]
    [SerializeField] private string requiredTag = "";

    private void OnTriggerEnter(Collider other)
    {
        // Filter by tag if specified
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
        {
            return;
        }

        if (targetGroup == null)
        {
            Debug.LogWarning("Target Group reference is missing on " + gameObject.name, this);
            return;
        }

        RemoveTarget(other.transform);
    }

    private void RemoveTarget(Transform targetTransform)
    {
        // Find if the transform exists in the target group
        int index = targetGroup.FindMember(targetTransform);

        if (index != -1)
        {
            targetGroup.RemoveMember(targetTransform);
        }
    }
    
}
