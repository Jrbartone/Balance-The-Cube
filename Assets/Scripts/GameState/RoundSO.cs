using UnityEngine;

[CreateAssetMenu(fileName = "NewRound", menuName = "Game/Round SO")]
public class RoundSO : ScriptableObject
{
    [Header("Round Conditions")]
    [Tooltip("Time in seconds the player must survive. Set to 0 if not timed.")]
    public float roundDuration;
    
    [Tooltip("Points required to complete the round. Set to 0 if no point goal.")]
    public int pointGoal;
}