using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Transform floatingTextSpawnPoint; // Position underneath or near the main score text (defaults to scoreText if unassigned)

    [Header("Animation Settings")]
    [SerializeField] private float tickDuration = 0.30f;  // How fast the score ticks up/down

    private int currentScore = 0;
    private int displayedScore = 0;
    
    // Queue and debounce controls to prevent overlapping chaos during fast increments
    private Queue<int> scoreQueue = new Queue<int>();
    private bool isProcessingQueue = false;

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
    }

    /// <summary>
    /// Increment the score by a specific amount.
    /// </summary>
    public void AddScore(int amount)
    {
        if (amount <= 0) return;
        scoreQueue.Enqueue(amount);
        CheckAndProcessQueue();
    }

    /// <summary>
    /// Decrement the score by a specific amount.
    /// </summary>
    public void SubtractScore(int amount)
    {
        if (amount <= 0) return;
        scoreQueue.Enqueue(-amount);
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

            // Trigger floating text via FloatingTextSpawner instance
            SpawnFloatingText(delta);

            // Smoothly tick the main score text from current to new value
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

            // Small buffer / debounce between queued changes so rapid inputs play sequentially clean
            yield return new WaitForSeconds(0.05f);
        }

        isProcessingQueue = false;
    }

    private void SpawnFloatingText(int delta)
    {
        if (FloatingTextSpawner.ScoreInstance == null) return;

        // Determine spawn position transform
        Transform spawnTransform = floatingTextSpawnPoint != null ? floatingTextSpawnPoint : (scoreText != null ? scoreText.transform : transform);
        
        // Format text string (e.g., "+50" or "-10")
        string textString = (delta > 0 ? "+" : "") + delta.ToString();

        // Pass to FloatingTextSpawner instance (relying on its built-in queue, positioning, and DOTween handling)
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