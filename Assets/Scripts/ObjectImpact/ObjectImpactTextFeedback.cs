using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ObjectImpactBroadcaster))]
public class ObjectImpactScoreFeedback : MonoBehaviour
{
    private enum MovementState
    {
        None,
        Slide,
        Air,
        Still
    }

    private ObjectImpactBroadcaster broadcaster;
    private TrackedTextHandle textHandle;
    
    private float slideTimer;
    private MovementState currentState = MovementState.None;

    private void Awake()
    {
        broadcaster = GetComponent<ObjectImpactBroadcaster>();
    }

    private void OnEnable()
    {
        broadcaster.OnImpact.AddListener(OnImpact);
        broadcaster.OnSlide.AddListener(OnSlide);
        broadcaster.OnAir.AddListener(OnAirUpdated);
        broadcaster.OnStillness.AddListener(OnStillness);
    }

    private void OnDisable()
    {
        broadcaster.OnImpact.RemoveListener(OnImpact);
        broadcaster.OnSlide.RemoveListener(OnSlide);
        broadcaster.OnAir.RemoveListener(OnAirUpdated);
        broadcaster.OnStillness.RemoveListener(OnStillness);
    }

    private void OnImpact(Collision collision, ObjectImpactBroadcaster.ImpactType type)
    {
        FloatingTextSpawner.Instance.SpawnText(gameObject, "bonk", transform.position);
    }

    private void OnSlide(float volume)
    {
        if (currentState == MovementState.Still)
        {
            ReleaseTextHandle();
        }

        currentState = MovementState.Slide;
        slideTimer += Time.deltaTime * 10f;

        string timerText = ((int)slideTimer).ToString();

        if (textHandle != null)
        {
            textHandle.SetText(timerText);
        }
        else
        {
            textHandle = FloatingTextSpawner.Instance.SpawnHoldText(timerText, transform);
        }
    }

    private void OnAirUpdated(float volume)
    {
        if (volume < 1f)
        {
            return;
        } 
        
        if (currentState != MovementState.Air)
        {
            ReleaseTextHandle();
        }
        
        currentState = MovementState.Air;

        if (textHandle == null)
        {
            textHandle = FloatingTextSpawner.Instance.SpawnHoldText("AIR", transform);
        } 
        else
        {
            textHandle.SetText("AIR");
        }
    }

    private void OnStillness()
    {
        if (currentState != MovementState.Still)
        {
            ReleaseTextHandle();
        }
        
        currentState = MovementState.Still;

        if (textHandle == null)
        {
            textHandle = FloatingTextSpawner.Instance.SpawnHoldText("STILL!", transform);
        }
    }

    private void ReleaseTextHandle()
    {
        if (textHandle != null)
        {
            FloatingTextSpawner.Instance.ReleaseText(textHandle);
            textHandle = null;
        }
    }
}