using UnityEngine;

public class SubtleRotationFollower : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The GameObject whose rotation will drive the movement.")]
    [SerializeField] private Transform targetObject;

    [Header("Movement Controls")]
    [Tooltip("Controls how subtle or pronounced the movement is.")]
    [SerializeField] private float movementScale = 0.005f;

    [Tooltip("How smoothly the object lerps to its target position.")]
    [SerializeField] private float lerpSpeed = 5f;

    private Vector3 initialPosition;

    void Start()
    {
        // Store the starting position as the baseline anchor point
        initialPosition = transform.position;
    }

    void Update()
    {
        if (targetObject == null) return;

        // Get normalized signed angles (-180 to 180) to prevent 0-360 snapping
        float xRotation = Mathf.DeltaAngle(0, targetObject.eulerAngles.x);
        float zRotation = Mathf.DeltaAngle(0, targetObject.eulerAngles.z);

        // Map Z rotation to X position, and X rotation to Y position
        float targetX = initialPosition.x + (zRotation * movementScale);
        float targetY = initialPosition.y + (xRotation * movementScale);

        // Keep the original Z depth position intact
        Vector3 targetPosition = new Vector3(targetX, targetY, initialPosition.z);

        // Smoothly lerp towards the calculated position
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
    }
}