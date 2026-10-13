using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Transform floatingTextSpawnPoint;

    [Header("Animation Settings")]
    [SerializeField] private float tickDuration = 0.30f;
    [Tooltip("Score changes at or below this amount will tick point-by-point and play the sound the exact amount of times.")]
    [SerializeField] private int lowNumberThreshold = 10;

    [Header("Audio Settings")]
    [SerializeField] private AudioCueSO tickerAudioCue;
    [Tooltip("Fastest time between repeated sound triggers when intensity is max (1.0). Also used as the step interval for low numbers.")]
    [SerializeField] private float minTickSoundInterval = 0.05f;
    [Tooltip("Slowest time between repeated sound triggers when intensity is low.")]
    [SerializeField] private float maxTickSoundInterval = 0.15f;

    [Header("Dynamic Shake Settings")]
    [SerializeField] private TMPTextShake scoreTextShake;
    [SerializeField] private float maxShakeStrength = 8f;
    [SerializeField] private float maxShakeSpeed = 45f;
    [SerializeField] private float shakeDecayRate = 10f;
    [SerializeField] private float shakeScaleMultiplier = 0.01f;

    [HideInInspector]
    public int currentScore = 0;
    private int displayedScore = 0;
    
    // Queue and state controls
    private Queue<int> scoreQueue = new Queue<int>();
    private bool isProcessingQueue = false;
    private int currentDeltaBeingProcessed = 0;

    // Runtime shake calculation values
    private float targetShakeIntensity = 0f;
    private float currentShakeIntensity = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        displayedScore = currentScore;
        UpdateScoreText();

        if (scoreTextShake == null && scoreText != null)
        {
            scoreTextShake = scoreText.GetComponent<TMPTextShake>();
        }
    }

    private void Start()
    {
        StartCoroutine(AudioTickerObserverRoutine());
    }

    private void Update()
    {
        // Smoothly interpolate shake intensity up or down every frame
        float decay = isProcessingQueue ? 20f : shakeDecayRate;
        currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, targetShakeIntensity, Time.deltaTime * decay);

        // Feed strength and speed into the TMPTextShake component based on activity level
        if (scoreTextShake != null)
        {
            if (currentShakeIntensity <= 0.01f)
            {
                scoreTextShake.SetShakeConfig(0f, 0f);
            }
            else
            {
                float strength = currentShakeIntensity * maxShakeStrength;
                float speed = Mathf.Lerp(15f, maxShakeSpeed, currentShakeIntensity);
                scoreTextShake.SetShakeConfig(strength, speed);
            }
        }

        // If queue is empty, fade target intensity to 0
        if (!isProcessingQueue)
        {
            targetShakeIntensity = 0f;
        }
    }

    public void AddScore(int amount)
    {
        if (amount <= 0) return;
        scoreQueue.Enqueue(amount);
        
        float shakeAdd = Mathf.Abs(amount) * shakeScaleMultiplier;
        targetShakeIntensity = Mathf.Clamp01(targetShakeIntensity + shakeAdd);
        
        CheckAndProcessQueue();
    }

    public void SubtractScore(int amount)
    {
        if (amount <= 0) return;
        scoreQueue.Enqueue(-amount);
        
        float shakeAdd = Mathf.Abs(amount) * shakeScaleMultiplier;
        targetShakeIntensity = Mathf.Clamp01(targetShakeIntensity + shakeAdd);
        
        CheckAndProcessQueue();
    }

    private void CheckAndProcessQueue()
    {
        if (!isProcessingQueue)
        {
            isProcessingQueue = true;
            StartCoroutine(ProcessScoreQueue());
        }
    }

    private IEnumerator ProcessScoreQueue()
    {
        while (scoreQueue.Count > 0)
        {
            currentDeltaBeingProcessed = scoreQueue.Dequeue();
            int startScore = currentScore;
            currentScore += currentDeltaBeingProcessed;

            SpawnFloatingText(currentDeltaBeingProcessed);

            int absDelta = Mathf.Abs(currentDeltaBeingProcessed);

            if (absDelta <= lowNumberThreshold)
            {
                // For low numbers, update the score point-by-point matching the audio ticks
                int step = (currentDeltaBeingProcessed > 0) ? 1 : -1;

                for (int i = 0; i < absDelta; i++)
                {
                    displayedScore += step;
                    UpdateScoreText();
                    yield return new WaitForSeconds(minTickSoundInterval);
                }
            }
            else
            {
                // For larger numbers, use smooth interpolation over tickDuration
                float elapsedTime = 0f;
                while (elapsedTime < tickDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedTime / tickDuration);
                    displayedScore = Mathf.RoundToInt(Mathf.Lerp(startScore, currentScore, t));
                    UpdateScoreText();
                    yield return null;
                }
            }

            displayedScore = currentScore;
            UpdateScoreText();

            // Clear current delta before the short pause between queue items
            currentDeltaBeingProcessed = 0;
            yield return new WaitForSeconds(0.05f);
        }

        isProcessingQueue = false;
        currentDeltaBeingProcessed = 0;
    }

    private IEnumerator AudioTickerObserverRoutine()
    {
        while (true)
        {
            // Wait silently until queue processing begins
            yield return new WaitUntil(() => isProcessingQueue);

            while (isProcessingQueue)
            {
                int delta = currentDeltaBeingProcessed;
                int absDelta = Mathf.Abs(delta);

                if (absDelta > 0 && absDelta <= lowNumberThreshold)
                {
                    // Integrated low-number rule: play the sound exactly 'absDelta' times at max speed
                    for (int i = 0; i < absDelta; i++)
                    {
                        if (tickerAudioCue != null && AudioManager.Instance != null)
                        {
                            AudioManager.Instance.PlaySFX(tickerAudioCue);
                        }
                        yield return new WaitForSeconds(Mathf.Lerp(maxTickSoundInterval, minTickSoundInterval, currentShakeIntensity));
                    }

                    // Wait until this specific low-number item finishes processing before looping again
                    yield return new WaitUntil(() => currentDeltaBeingProcessed != delta);
                }
                else
                {
                    // Standard continuous audio ticker routine for larger numbers
                    if (tickerAudioCue != null && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(tickerAudioCue);
                    }

                    float currentInterval = Mathf.Lerp(maxTickSoundInterval, minTickSoundInterval, currentShakeIntensity);
                    yield return new WaitForSeconds(currentInterval);
                }
            }
        }
    }

    private void SpawnFloatingText(int delta)
    {
        if (FloatingTextSpawner.ScoreInstance == null) return;

        Transform spawnTransform = floatingTextSpawnPoint != null ? floatingTextSpawnPoint : (scoreText != null ? scoreText.transform : transform);
        string textString = (delta > 0 ? "+" : "") + delta.ToString();
        FloatingTextSpawner.ScoreInstance.SpawnText(spawnTransform, textString, spawnTransform.position);
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = displayedScore.ToString();
        }
    }
}