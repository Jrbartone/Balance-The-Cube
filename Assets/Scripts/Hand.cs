using UnityEngine;
using UnityEngine.InputSystem;

public class Hand : MonoBehaviour
{
    public enum Directionality {
        LEFT,
        RIGHT
    }

    public Directionality directionality = Directionality.LEFT;
    public GameObject handObject;
    private Transform handObjectParent;
    private Vector3 handObjectStartingLocalPosition;
    private Quaternion handObjectStartingLocalRotation;
    private InputAction activateHandAction;
    private InputAction moveHandAction;
    private float isHandActivated;
    private float input;
    private float _currentInput;

    // Eventually read this value from settings.
    [SerializeField] private float keyboardInputSpeed = 5f;
    [SerializeField] private float decelerationSpeed = 10f;

    [Header("Arc Settings")]
    [Tooltip("The pivot center of the half circle.")]
    public Transform centerPoint;

    [Tooltip("Radius of the half circle arc.")]
    public float radius = 5.0f;

    [Header("Smoothing (Optional)")]
    [Tooltip("Speed to lerp towards the target analog position. Set to 0 for instant response.")]
    public float lerpSpeed = 10f;

    [Header("Hand Motion Settings")]
    [SerializeField] private Transform restTarget;

    [Tooltip("Speed at which the hand object lerps to target position or back home.")]
    [SerializeField] private float handFollowSpeed = 15f;
    
    [Tooltip("Distance threshold to consider the hand returned back to its local origin.")]
    [SerializeField] private float returnThreshold = 0.001f;

    private bool isReturningHome = false;
    // Rotation offset to flip backward-facing mesh (-Z) forward (+Z)
    private static readonly Quaternion BackwardZOffset = Quaternion.Euler(0f, 180f, 0f);

    void Start()
    {
        if (handObject != null)
        {
            handObjectParent = handObject.transform.parent;
            handObjectStartingLocalPosition = handObject.transform.localPosition;
            handObjectStartingLocalRotation = handObject.transform.localRotation;
        }
        activateHandAction = directionality == Directionality.LEFT 
            ? InputSystem.actions.FindAction("ActivateLeftHand") 
            : InputSystem.actions.FindAction("ActivateRightHand");
            
        moveHandAction = directionality == Directionality.LEFT 
            ? InputSystem.actions.FindAction("MoveLeftHand") 
            : InputSystem.actions.FindAction("MoveRightHand");
    }

    void Update()
    {
        handleInput();
        handleHandMotion();
    }

    void handleInput(){
        isHandActivated = activateHandAction.ReadValue<float>();
        if(isHandActivated == 0) {
            input = Mathf.MoveTowards(input, 0f, decelerationSpeed * Time.deltaTime);
        } else {
            if(isGamepad()){
                input = moveHandAction.ReadValue<float>();
            } else {
                float rawInput = moveHandAction.ReadValue<float>();
                input = Mathf.Clamp(input + (rawInput * keyboardInputSpeed * Time.deltaTime), -1f, 1f);
            }
        }

        // Debug.Log(directionality + " isHandActivated: " + isHandActivated);
        // Debug.Log(directionality + " input: " + input);

        // Smooth input transition (Optional - omit if passing pre-smoothed input)
        if (lerpSpeed > 0f)
        {
            _currentInput = Mathf.Lerp(_currentInput, input, Time.deltaTime * lerpSpeed);
        }
        else
        {
            _currentInput = input;
        }

        UpdateArcPositionAndRotation(_currentInput);
    }

    void handleHandMotion()
    {
        if (handObject == null) return;

        bool active = isHandActivated > 0f;

        // Cache animator component to avoid repeated GetComponent calls
        if (handObject.TryGetComponent<Animator>(out var animator))
        {
            animator.SetBool("active", active);
        }

        if (active)
        {
            // Target rotation aligned forward, adjusted for backward -Z mesh orientation
            Quaternion targetWorldRot = transform.rotation * BackwardZOffset;

            // Smoothly move/rotate towards active target position/rotation
            handObject.transform.position = Vector3.Lerp(
                handObject.transform.position, 
                transform.position, 
                Time.deltaTime * handFollowSpeed
            );
            
            handObject.transform.rotation = Quaternion.Slerp(
                handObject.transform.rotation, 
                targetWorldRot, 
                Time.deltaTime * handFollowSpeed
            );
        }
        else
        {
            if (restTarget == null) return;

            var trueHandFollowSpeed = Vector3.Distance(handObject.transform.position, restTarget.position) < .2f ? 200f : handFollowSpeed;

            // Smoothly move/rotate towards the rest target position/rotation
            handObject.transform.position = Vector3.Lerp(
                handObject.transform.position, 
                restTarget.position, 
                Time.deltaTime * trueHandFollowSpeed
            );
            
            handObject.transform.rotation = Quaternion.Slerp(
                handObject.transform.rotation, 
                restTarget.rotation, 
                Time.deltaTime * trueHandFollowSpeed
            );
        }
    }

    bool isGamepad()
    {
        return activateHandAction.activeControl != null && activateHandAction.activeControl.device is Gamepad;
    }

    private void UpdateArcPositionAndRotation(float input)
    {
        // Map input (-1 to 1) to angle range in radians (-PI/2 to PI/2)
        // input 0   -> angle 0 (Center/Forward)
        // input 1   -> angle +PI/2 (Front tip)
        // input -1  -> angle -PI/2 (Back tip)
        float angleRad = input * (Mathf.PI * 0.5f);

        // Adjust for side flip (Right side curves toward +X, Left side curves toward -X)
        float sideSign = (directionality == Directionality.LEFT) ? 1f : -1f;

        // Calculate offset position in local arc space relative to centerPoint:
        // Forward (Z) offset = R * sin(angle)
        // Lateral (X) offset = R * (1 - cos(angle)) * sideSign
        float localZ = radius * Mathf.Sin(angleRad);
        float localX = radius * (1f - Mathf.Cos(angleRad)) * sideSign;

        Vector3 localOffset = new Vector3(localX, 0f, localZ);
        
        // Transform offset into world position using centerPoint's orientation
        transform.position = centerPoint.position + (centerPoint.rotation * localOffset);

        // Calculate Forward Tangent vector along the arc path
        // Derivative of position w.r.t angle gives tangent vector (sideSign * sin(angle), 0, cos(angle))
        Vector3 localTangent = new Vector3(sideSign * Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad));
        Vector3 worldTangent = centerPoint.rotation * localTangent;

        // Align the object's Z-axis (forward vector) to the path's tangent
        if (worldTangent.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(worldTangent, centerPoint.up);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (centerPoint == null) return;

        Gizmos.color = Color.cyan;
        int segments = 30;
        float sideSign = (directionality == Directionality.LEFT) ? 1f : -1f;
        Vector3 previousPos = centerPoint.position;

        for (int i = 0; i <= segments; i++)
        {
            float t = Mathf.Lerp(-1f, 1f, (float)i / segments);
            float angleRad = t * (Mathf.PI * 0.5f);
            
            float localZ = radius * Mathf.Sin(angleRad);
            float localX = radius * (1f - Mathf.Cos(angleRad)) * sideSign;

            Vector3 point = centerPoint.position + centerPoint.rotation * new Vector3(localX, 0f, localZ);

            if (i > 0)
            {
                Gizmos.DrawLine(previousPos, point);
            }
            previousPos = point;
        }
    }

}
