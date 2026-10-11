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

    [Header("Dynamic Shake Settings")]
    [SerializeField] private TMPTextShake scoreTextShake; // Reference to the shake script on your score text
    [SerializeField] private float maxShakeStrength = 8f;
    [SerializeField] private float maxShakeSpeed = 45f;
    [SerializeField] private float shakeDecayRate = 10f; // How fast the shake calms down when scoring stops
    [SerializeField] private float shakeScaleMultiplier = 0.01f; // How much each point of score increases the shake intensity

    [HideInInspector]
    public int currentScore = 0;
    private int displayedScore = 0;
    
    // Queue and debounce controls
    private Queue<int> scoreQueue = new Queue<int>();
    private bool isProcessingQueue = false;

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

        // Auto-grab shake component if not assigned
        if (scoreTextShake == null && scoreText != null)
        {
            scoreTextShake = scoreText.GetComponent<TMPTextShake>();
        }
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
        
        // Scale shake intensity up dynamically based on the magnitude of the score increase
        float shakeAdd = Mathf.Abs(amount) * shakeScaleMultiplier;
        targetShakeIntensity = Mathf.Clamp01(targetShakeIntensity + shakeAdd);
        
        CheckAndProcessQueue();
    }

    public void SubtractScore(int amount)
    {
        if (amount <= 0) return;
        scoreQueue.Enqueue(-amount);
        
        // Scale shake intensity up dynamically based on the magnitude of the score decrease
        float shakeAdd = Mathf.Abs(amount) * shakeScaleMultiplier;
        targetShakeIntensity = Mathf.Clamp01(targetShakeIntensity + shakeAdd);
        
        CheckAndProcessQueue();
    }

    private void CheckAndProcessQueue()
    {
        if (!isProcessingQueue)
        {
            StartCoroutine(ProcessScoreQueue());
        }
    }

    private IEnumerator ProcessScoreQueue()
    {
        isProcessingQueue = true;

        while (scoreQueue.Count > 0)
        {
            int delta = scoreQueue.Dequeue();
            int startScore = currentScore;
            currentScore += delta;

            SpawnFloatingText(delta);

            float elapsedTime = 0f;
            while (elapsedTime < tickDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / tickDuration);
                displayedScore = Mathf.RoundToInt(Mathf.Lerp(startScore, currentScore, t));
                UpdateScoreText();
                yield return null;
            }

            displayedScore = currentScore;
            UpdateScoreText();

            yield return new WaitForSeconds(0.05f);
        }

        isProcessingQueue = false;
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