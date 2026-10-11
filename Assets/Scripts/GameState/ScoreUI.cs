using UnityEngine;
using TMPro;
using DG.Tweening;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI thresholdText; // Displays "/ goalAmount"
    
    [Header("Material Swap Settings")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material goalMetMaterial;

    [Header("DOTween Punch Settings")]
    [SerializeField] private float punchScale = 0.2f;
    [SerializeField] private float punchDuration = 0.2f;

    private TMPTextShake textShake;
    private bool isGoalMet = false;
    private Tween punchTween;

    private void Awake()
    {
        if (thresholdText != null)
        {
            textShake = thresholdText.GetComponent<TMPTextShake>();
        }
    }

    public void Initialize(int pointGoal)
    {
        if (thresholdText != null)
        {
            thresholdText.text = $"/ {pointGoal}";
            thresholdText.gameObject.SetActive(pointGoal > 0);
        }

        isGoalMet = false;
        UpdateMaterial(false);
        UpdateShakeSettings(false);
    }

    public void CheckThreshold(int currentScore, int pointGoal)
    {
        if (pointGoal <= 0) return;

        bool met = currentScore >= pointGoal;
        if (met != isGoalMet)
        {
            isGoalMet = met;
            UpdateMaterial(isGoalMet);
            UpdateShakeSettings(isGoalMet);

            // Punch the text the exact moment the threshold is crossed
            if (isGoalMet)
            {
                PunchThreshold();
            }
        }
    }

    private void UpdateMaterial(bool met)
    {
        if (thresholdText == null) return;

        Material targetMat = met ? goalMetMaterial : normalMaterial;
        if (targetMat != null)
        {
            thresholdText.fontSharedMaterial = targetMat;
        }
    }

    private void UpdateShakeSettings(bool met)
    {
        if (textShake == null) return;

        if (met)
        {
            textShake.SetShakeConfig(3f, 1f);
        }
        else
        {
            //textShake.SetShakeConfig(2f, 20f);
            textShake.SetShakeConfig(0f, 0f);
        }
    }

    public void PunchThreshold()
    {
        if (thresholdText == null) return;

        punchTween?.Kill();
        thresholdText.transform.localScale = Vector3.one;
        punchTween = thresholdText.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 10, 1);
    }
}