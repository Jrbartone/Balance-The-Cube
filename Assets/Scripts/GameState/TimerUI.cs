using UnityEngine;
using TMPro;
using DG.Tweening;

public class TimerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    
    [Header("DOTween Punch Settings")]
    [SerializeField] private float punchScale = 0.2f;
    [SerializeField] private float punchDuration = 0.2f;

    private Tween punchTween;

    public void UpdateTimerDisplay(float remainingTime)
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void PunchTimer()
    {
        if (timerText == null) return;

        punchTween?.Kill();
        timerText.transform.localScale = Vector3.one;
        punchTween = timerText.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 10, 1);
    }

    public void SetVisible(bool isVisible)
    {
        if (timerText != null)
            timerText.gameObject.SetActive(isVisible);
    }
}