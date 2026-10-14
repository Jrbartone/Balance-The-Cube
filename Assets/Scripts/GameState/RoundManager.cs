using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Playables;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Round Configuration")]
    [SerializeField] private List<RoundSO> rounds = new List<RoundSO>();
    [SerializeField] private int currentRoundIndex = 0;

    [Header("UI References")]
    [SerializeField] private TimerUI timerUI;
    [SerializeField] private ScoreUI scoreUI;

    [Header("Audio Settings")]
    [SerializeField] private AudioCueSO timerTickAudioCue;

    [Header("Round Cutscenes")]
    [SerializeField] private PlayableDirector startRoundCutscene;
    [SerializeField] private PlayableDirector endRoundCutscene;

    private RoundSO currentRound;
    private float remainingTime;
    private bool isRoundActive = false;
    private bool timerStarted = false;

    public event System.Action OnRoundCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

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
        
        if (startRoundCutscene != null && CutsceneManager.Instance != null)
        {
            CutsceneManager.Instance.PlayCutscene(startRoundCutscene, () => {});
        }
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
        timerStarted = false; // Reset timer flag until explicitly started

        if (currentRound.roundDuration > 0)
        {
            remainingTime = currentRound.roundDuration;
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

    public void StartTimer()
    {
        if (timerStarted) return;
        
        timerStarted = true;

        if (currentRound != null && currentRound.roundDuration > 0)
        {
            remainingTime = currentRound.roundDuration;
            timerUI?.SetVisible(true);
            timerUI?.UpdateTimerDisplay(remainingTime);
        }
    }

    private void Update()
    {
        if (!isRoundActive || currentRound == null) return;

        // 1. Maintain and Update Timer (only if timer has explicitly started)
        if (currentRound.roundDuration > 0 && timerStarted)
        {
            if (remainingTime > 0)
            {
                float previousTime = remainingTime;
                remainingTime -= Time.deltaTime;
                remainingTime = Mathf.Max(0f, remainingTime);

                // Check for whole second integer boundaries crossed this frame
                int previousSecond = Mathf.FloorToInt(previousTime);
                int currentSecond = Mathf.FloorToInt(remainingTime);

                // Play tick and punch timer when crossing a second boundary (including the final tick to 0)
                if (currentSecond != previousSecond && previousTime > 0f)
                {
                    if (timerTickAudioCue != null && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(timerTickAudioCue);
                    }
                    timerUI?.PunchTimer();
                }

                timerUI?.UpdateTimerDisplay(remainingTime);
            }
            else
            {
                timerUI?.UpdateTimerDisplay(0f);
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

        // 3. Evaluate Round Completion Conditions (Only evaluate if timer has started when a duration is required)
        if (!timerStarted && currentRound.roundDuration > 0) return;

        EvaluateRoundCompletion(currentScore);
    }

    private void EvaluateRoundCompletion(int currentScore)
    {
        bool timeExpired = currentRound.roundDuration > 0 && remainingTime <= 0f;
        bool scoreReached = currentRound.pointGoal > 0 && currentScore >= currentRound.pointGoal;

        bool roundComplete = false;

        // Logic depending on whether the round uses time, points, or both
        if (currentRound.roundDuration > 0 && currentRound.pointGoal > 0)
        {
            if (timeExpired && scoreReached) roundComplete = true;
        }
        else if (currentRound.roundDuration > 0 && currentRound.pointGoal == 0)
        {
            if (timeExpired) roundComplete = true;
        }
        else if (currentRound.pointGoal > 0 && currentRound.roundDuration == 0)
        {
            if (scoreReached) roundComplete = true;
        }

        if (roundComplete && isRoundActive)
        {
            isRoundActive = false;
            
            if (endRoundCutscene != null && CutsceneManager.Instance != null)
            {
                CutsceneManager.Instance.PlayCutscene(endRoundCutscene, () => CompleteRound());
            }
            else
            {
                CompleteRound();
            }
        }
    }

    private void CompleteRound()
    {
        timerStarted = false;
        Debug.Log($"Round {currentRoundIndex + 1} Completed!");
        OnRoundCompleted?.Invoke();
    }

    public void StartNextRound()
    {
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