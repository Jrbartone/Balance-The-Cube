using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ObjectImpactBroadcaster))]
public class ObjectImpactTextFeedback : MonoBehaviour
{
    private ObjectImpactBroadcaster broadcaster;
    private TrackedTextHandle textHandle;
    private float slideTimer = 0f;

    private void Awake() => broadcaster = GetComponent<ObjectImpactBroadcaster>();

    private void OnEnable()
    {
        broadcaster.OnImpact.AddListener(OnImpact);
        broadcaster.OnSlide.AddListener(OnSlide);
        broadcaster.OnAir.AddListener(OnAirUpdated);
    }

    private void OnDisable()
    {
        broadcaster.OnImpact.RemoveListener(OnImpact);
        broadcaster.OnSlide.RemoveListener(OnSlide);
        broadcaster.OnAir.RemoveListener(OnAirUpdated);
    }

    private void OnImpact(Collision collision, ObjectImpactBroadcaster.ImpactType type)
    {
        FloatingTextSpawner.Instance.SpawnText(gameObject, "bonk", transform.position);
    }

    private void OnSlide(float volume)
    {
        slideTimer += Time.deltaTime * 10f;

        if (volume < 0.01f)
        {
            if (textHandle != null)
            {
                FloatingTextSpawner.Instance.ReleaseText(textHandle);
                textHandle = null;
            }
            return;
        }

        if (textHandle != null)
        {
            textHandle.SetText(((int)slideTimer).ToString());
            return;
        }

        textHandle = FloatingTextSpawner.Instance.SpawnHoldText(((int)slideTimer).ToString(), transform);
    }

    private void OnAirUpdated(float volume){
        // TODO
    }
}