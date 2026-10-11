using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SubtleCanvasRotationFollower : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The GameObject whose rotation will drive the movement.")]
    [SerializeField] private Transform targetObject;

    [Header("Movement Controls")]
    [Tooltip("Controls how subtle or pronounced the movement is in canvas pixels/units.")]
    [SerializeField] private float movementScale = 0.5f;

    [Tooltip("How smoothly the UI element lerps to its target position.")]
    [SerializeField] private float lerpSpeed = 5f;

    private RectTransform rectTransform;
    private Vector2 initialAnchoredPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Start()
    {
        if (rectTransform != null)
        {
            // Store the starting anchored position as the baseline anchor point
            initialAnchoredPosition = rectTransform.anchoredPosition;
        }
    }

    void Update()
    {
        if (targetObject == null || rectTransform == null) return;

        // Get normalized signed angles (-180 to 180) to prevent 0-360 snapping
        float xRotation = Mathf.DeltaAngle(0, targetObject.eulerAngles.x);
        float zRotation = Mathf.DeltaAngle(0, targetObject.eulerAngles.z);

        // Map Z rotation to X anchored position, and X rotation to Y anchored position
        float targetX = initialAnchoredPosition.x + (zRotation * movementScale);
        float targetY = initialAnchoredPosition.y - (xRotation * movementScale);

        Vector2 targetAnchoredPosition = new Vector2(targetX, targetY);

        // Smoothly lerp towards the calculated anchored position
        rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetAnchoredPosition, Time.deltaTime * lerpSpeed);
    }
}