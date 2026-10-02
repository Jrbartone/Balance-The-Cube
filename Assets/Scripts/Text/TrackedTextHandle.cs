using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// Runtime component attached to tracking instances to follow transforms in LateUpdate.
/// </summary>
public class TrackedTextHandle : MonoBehaviour
{
    public RectTransform RectTransform { get; private set; }
    public TextMeshProUGUI TmpText { get; private set; }
    public Transform TargetTransform { get; private set; }
    public Vector3 WorldOffset { get; private set; }
    public Canvas Canvas { get; private set; }
    public Camera WorldCamera { get; private set; }
    public FloatingTextSpawner Spawner { get; private set; }

    public bool IsTracking { get; private set; } = true;
    private float xDirection;
    private Vector2 baseCanvasPos;

    public void Initialize(
        RectTransform rectTransform,
        TextMeshProUGUI tmpText,
        Transform targetTransform,
        Vector3 worldOffset,
        Canvas canvas,
        Camera worldCamera,
        FloatingTextSpawner spawner)
    {
        RectTransform = rectTransform;
        TmpText = tmpText;
        TargetTransform = targetTransform;
        WorldOffset = worldOffset;
        Canvas = canvas;
        WorldCamera = worldCamera;
        Spawner = spawner;
        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        Vector3 worldPoint = targetTransform.position + worldOffset;
        baseCanvasPos = spawner.WorldToCanvasPosition(Canvas, RectTransform, worldPoint, cam);
        xDirection = baseCanvasPos.x < 0 ? -1f : 1f;
    }

    private void LateUpdate()
    {
        if (!IsTracking) return;

        if (TargetTransform == null)
        {
            // Target was destroyed while tracking; automatically trigger release animation
            Release();
            return;
        }

        UpdateCanvasPosition();
    }

    /// <summary>
    /// Updates the displayed text while the handle is visible and tracking.
    /// </summary>
    /// <param name="newText">The updated text string to show.</param>
    public void SetText(string newText)
    {
        if (TmpText != null)
        {
            TmpText.text = newText;
        }
    }

    public void UpdateCanvasPosition()
    {
        if (Canvas == null || RectTransform == null) return;

        Camera cam = WorldCamera != null ? WorldCamera : Camera.main;

        Vector3 WorldPoint = TargetTransform.position + WorldOffset;
        baseCanvasPos = Spawner.WorldToCanvasPosition(Canvas, RectTransform, WorldPoint, cam);
        
        Vector2 offsetVector = new Vector2(Spawner.horizontalOffset * xDirection, Spawner.verticalOffset);

        RectTransform.anchoredPosition = baseCanvasPos + offsetVector;
    }

    public void StopTracking()
    {
        IsTracking = false;
    }

    public void Release()
    {
        if (Spawner != null)
        {
            Spawner.ReleaseText(this);
        }
    }
}