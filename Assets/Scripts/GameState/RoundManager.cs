using UnityEngine;
using System.Collections.Generic;

public class RoundManager : MonoBehaviour
{
    [Header("Round Configuration")]
    [SerializeField] private List<RoundSO> rounds = new List<RoundSO>();
    [SerializeField] private int currentRoundIndex = 0;

    [Header("UI References")]
    [SerializeField] private TimerUI timerUI;
    [SerializeField] private ScoreUI scoreUI;

    private RoundSO currentRound;
    private float remainingTime;
    private bool isRoundActive = false;
    private int lastWholeSecond = -1;

    public event System.Action OnRoundCompleted;

    private void Start()
    {
        if (rounds.Count > 0)
        {
            OnRoundStart();
        }
    }

    public void OnRoundStart()
    {
        StartRound(currentRoundIndex);
    }

    public void StartRound(int index)
    {
        if (index < 0 || index >= rounds.Count)
        {
            Debug.LogWarning("Round index out of range!");
            return;
        }

        currentRoundIndex = index;
        currentRound = rounds[currentRoundIndex];
        isRoundActive = true;

        // Initialize Timer
        if (currentRound.roundDuration > 0)
        {
            remainingTime = currentRound.roundDuration;
            lastWholeSecond = Mathf.CeilToInt(remainingTime);
            timerUI?.SetVisible(true);
            timerUI?.UpdateTimerDisplay(remainingTime);
        }
        else
        {
            timerUI?.SetVisible(false);
        }

        // Initialize Score Threshold UI
        scoreUI?.Initialize(currentRound.pointGoal);

        Debug.Log($"Round {currentRoundIndex + 1} Started.");
    }

    private void Update()
    {
        if (!isRoundActive || currentRound == null) return;

        // 1. Maintain and Update Timer
        if (currentRound.roundDuration > 0)
        {
            if (remainingTime > 0)
            {
                remainingTime -= Time.deltaTime;
                timerUI?.UpdateTimerDisplay(Mathf.Max(0f, remainingTime));

                int currentWholeSecond = Mathf.CeilToInt(remainingTime);
                if (currentWholeSecond != lastWholeSecond)
                {
                    lastWholeSecond = currentWholeSecond;
                    timerUI?.PunchTimer();
                }
            }
        }

        // 2. Check Score Threshold via Singleton ScoreManager
        int currentScore = 0;
        if (ScoreManager.Instance != null)
        {
            currentScore = ScoreManager.Instance.currentScore;
            if (currentRound.pointGoal > 0)
            {
                scoreUI?.CheckThreshold(currentScore, currentRound.pointGoal);
            }
        }

        // 3. Evaluate Round Completion Conditions
        EvaluateRoundCompletion(currentScore);
    }

    private void EvaluateRoundCompletion(int currentScore)
    {
        bool timeExpired = currentRound.roundDuration > 0 && remainingTime <= 0;
        bool scoreReached = currentRound.pointGoal > 0 && currentScore >= currentRound.pointGoal;

        bool roundComplete = false;

        // Logic depending on whether the round uses time, points, or both
        if (currentRound.roundDuration > 0 && currentRound.pointGoal > 0)
        {
            // If both are set, require surviving the time AND hitting the score goal
            if (timeExpired && scoreReached) roundComplete = true;
        }
        else if (currentRound.roundDuration > 0)
        {
            if (timeExpired) roundComplete = true;
        }
        else if (currentRound.pointGoal > 0)
        {
            if (scoreReached) roundComplete = true;
        }

        if (roundComplete)
        {
            CompleteRound();
        }
    }

    private void CompleteRound()
    {
        isRoundActive = false;
        Debug.Log($"Round {currentRoundIndex + 1} Completed!");
        OnRoundCompleted?.Invoke();

        // Advance to next round if available
        if (currentRoundIndex + 1 < rounds.Count)
        {
            StartRound(currentRoundIndex + 1);
        }
        else
        {
            Debug.Log("All rounds finished!");
        }
    }
}