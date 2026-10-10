using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ObjectImpactBroadcaster))]
public class ObjectImpactScoreFeedback : MonoBehaviour
{
    private ObjectImpactBroadcaster broadcaster;
    private TrackedTextHandle textHandle;
    private ObjectModifierController modifierController;

    private void Awake()
    {
        broadcaster = GetComponent<ObjectImpactBroadcaster>();
        modifierController = broadcaster != null ? broadcaster.ModifierController : GetComponent<ObjectModifierController>();
    }

    private void OnEnable()
    {
        broadcaster.OnImpact.AddListener(OnImpact);
        broadcaster.OnApex.AddListener(OnApex);
        broadcaster.OnStateTransition.AddListener(OnStateTransition);
        broadcaster.OnStateTick.AddListener(OnStateTick);

        if (modifierController == null && broadcaster != null)
        {
            modifierController = broadcaster.ModifierController;
        }
    }

    private void OnDisable()
    {
        broadcaster.OnImpact.RemoveListener(OnImpact);
        broadcaster.OnApex.RemoveListener(OnApex);
        broadcaster.OnStateTransition.RemoveListener(OnStateTransition);
        broadcaster.OnStateTick.RemoveListener(OnStateTick);
    }

    private bool HasStatePoints(ObjectImpactBroadcaster.MovementState state)
    {
        if (modifierController == null) return false;

        switch (state)
        {
            case ObjectImpactBroadcaster.MovementState.Slide:
                return modifierController.TotalPointsPerSlideSecond != 0;
            case ObjectImpactBroadcaster.MovementState.Air:
                return modifierController.TotalPointsPerAirSecond != 0;
            case ObjectImpactBroadcaster.MovementState.Still:
                return modifierController.TotalPointsPerStillnessSecond != 0;
            case ObjectImpactBroadcaster.MovementState.None:
            default:
                return false;
        }
    }

    private void OnStateTransition(ObjectImpactBroadcaster.MovementState oldState, ObjectImpactBroadcaster.MovementState newState, float finalTimeInOldState)
    {
        if((int)finalTimeInOldState == 0)
        {
            return;
        }
        if (oldState != ObjectImpactBroadcaster.MovementState.None && HasStatePoints(oldState))
        {
            ScoreManager.Instance.AddScore((int)finalTimeInOldState);
        }

        ReleaseTextHandle();
    }

    private void OnStateTick(ObjectImpactBroadcaster.MovementState state, float timeInState, float volume)
    {
        if (!HasStatePoints(state))
        {
            return;
        }

        string timerText = ((int)timeInState).ToString();
        if ((int)timeInState == 0)
        {
            return;   
        }

        if (textHandle != null)
        {
            textHandle.SetText(timerText);
        }
        else
        {
            textHandle = FloatingTextSpawner.CubeInstance.SpawnHoldText(timerText, transform);
        }
    }

    private void OnImpact(Collision collision, ObjectImpactBroadcaster.ImpactType type)
    {
        if (modifierController == null || modifierController.TotalPointsPerImpact == 0)
        {
            return;
        }
        
        FloatingTextSpawner.CubeInstance.SpawnText(gameObject, modifierController.TotalPointsPerImpact.ToString(), transform.position);
        ScoreManager.Instance.AddScore(modifierController.TotalPointsPerImpact);
    }

    private void OnApex()
    {
        if (modifierController == null || modifierController.TotalPointsPerHeightUnit == 0)
        {
            return;
        }
        
        FloatingTextSpawner.CubeInstance.SpawnText(gameObject, modifierController.TotalPointsPerHeightUnit.ToString(), transform.position);
        ScoreManager.Instance.AddScore(modifierController.TotalPointsPerHeightUnit);
    }

    private void ReleaseTextHandle()
    {
        if (textHandle != null)
        {
            FloatingTextSpawner.CubeInstance.ReleaseText(textHandle);
            textHandle = null;
        }
    }
}