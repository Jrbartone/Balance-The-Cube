using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;

[RequireComponent(typeof(TextMeshProUGUI))]
public class CountdownTimer : MonoBehaviour
{
    [Header("Countdown Settings")]
    [SerializeField] private int startNumber = 3;
    [SerializeField] private float timeStep = 1.0f; // Seconds between numbers

    [Header("DOTween Animation Settings")]
    [SerializeField] private float punchScaleStrength = 0.5f;
    [SerializeField] private float animationDuration = 0.3f;

    [Header("Audio Settings")]
    [SerializeField] private AudioCueSO timerTickAudioCue;
    [SerializeField] private AudioCueSO timerGoAudioCue;

    private TextMeshProUGUI tmProText;

    void Awake()
    {
        tmProText = GetComponent<TextMeshProUGUI>();
    }

    void Start()
    {
        StartCoroutine(RunCountdown());
    }

    private IEnumerator RunCountdown()
    {
        int currentNumber = startNumber;
        while (currentNumber > 0)
        {
            // Update text and play sound/animation
            tmProText.text = currentNumber.ToString();
            TriggerMunchAnimation();
            if (timerTickAudioCue != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(timerTickAudioCue);
            }

            yield return new WaitForSeconds(timeStep);
            currentNumber--;
        }

        // Optional: Show "GO!" or finish state when it hits 0
        tmProText.text = "CUBE";
        TriggerMunchAnimation();
        if (timerGoAudioCue != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(timerGoAudioCue);
        }

        // Wait a brief moment before disabling or triggering the next event
        yield return new WaitForSeconds(timeStep);
        
        // Example: Disable the text object or trigger game start
        gameObject.SetActive(false);
    }

    private void TriggerMunchAnimation()
    {
        // Kill any ongoing tweens on the transform to avoid conflicts
        tmProText.transform.DOKill();
        
        // Reset scale just in case, then punch it
        tmProText.transform.localScale = Vector3.one;
        tmProText.transform.DOPunchScale(Vector3.one * punchScaleStrength, animationDuration, 10, 1);
    }
}