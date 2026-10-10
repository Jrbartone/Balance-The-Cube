using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingTextSpawner : MonoBehaviour
{

    public enum SpawnerType
    {
        Cube,
        Score
    }

    public SpawnerType spawnerType;
    public static FloatingTextSpawner CubeInstance { get; private set; }
    public static FloatingTextSpawner ScoreInstance { get; private set; }


    [Header("Prefab & Hierarchy")]
    [SerializeField] private GameObject floatingTextPrefab;
    [SerializeField] private Canvas targetCanvas;

    [Header("Animation Settings")]
    [SerializeField] private float punchDuration = 0.3f;
    [SerializeField] private Vector3 punchScale = new Vector3(0.3f, 0.3f, 0.3f);
    [SerializeField] private float floatDistance = 100f; // UI Canvas units
    [SerializeField] public float horizontalOffset = 40f; // UI Canvas X offset towards center
    [SerializeField] public float verticalOffset = 20f; // Base Canvas Y offset above world position
    [SerializeField] private float floatDuration = 1.2f;
    [SerializeField] private Ease floatEase = Ease.OutQuad;

    [Header("Queue Settings")]
    [SerializeField] private float delayBetweenSpawns = 0.15f;

    // Per-object request queue and active processing coroutines
    private readonly Dictionary<GameObject, Queue<SpawnRequest>> _objectQueues = new Dictionary<GameObject, Queue<SpawnRequest>>();
    private readonly Dictionary<GameObject, Coroutine> _processingRoutines = new Dictionary<GameObject, Coroutine>();

    // Maps target GameObject to its active TrackedTextHandle for side checking
    private readonly Dictionary<GameObject, TrackedTextHandle> _activeHoldTexts = new Dictionary<GameObject, TrackedTextHandle>();

    private struct SpawnRequest
    {
        public GameObject Source;
        public string Text;
        public Vector3 WorldPosition;
        public Canvas TargetCanvas;
        public Camera WorldCamera;

        public SpawnRequest(GameObject source, string text, Vector3 worldPosition, Canvas targetCanvas, Camera worldCamera)
        {
            Source = source;
            Text = text;
            WorldPosition = worldPosition;
            TargetCanvas = targetCanvas;
            WorldCamera = worldCamera;
        }
    }

    private void Awake()
    {
        // Assign the instances based on the spawner type
        if (spawnerType == SpawnerType.Cube)
        {
            if (CubeInstance == null) CubeInstance = this;
            else Destroy(gameObject);
        }
        else if (spawnerType == SpawnerType.Score)
        {
            if (ScoreInstance == null) ScoreInstance = this;
            else Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);
    }

    
    public void SpawnText(GameObject source, string text, Vector3 worldPosition, Camera worldCamera = null)
    {
        if (source == null) return;

        if (!_objectQueues.TryGetValue(source, out Queue<SpawnRequest> queue))
        {
            queue = new Queue<SpawnRequest>();
            _objectQueues[source] = queue;
        }

        queue.Enqueue(new SpawnRequest(source, text, worldPosition, targetCanvas, worldCamera));

        // If a routine reference exists but is no longer running (or was left dangling), clear it
        if (_processingRoutines.TryGetValue(source, out Coroutine existingRoutine) && existingRoutine == null)
        {
            _processingRoutines.Remove(source);
        }

        // Start a new routine if none is currently active
        if (!_processingRoutines.ContainsKey(source))
        {
            _processingRoutines[source] = StartCoroutine(ProcessObjectQueueRoutine(source));
        }
    }

    public void SpawnText(Transform sourceTransform, string text, Vector3 worldPosition, Camera worldCamera = null)
    {
        if (sourceTransform != null)
        {
            SpawnText(sourceTransform.gameObject, text, worldPosition, worldCamera);
        }
    }

    public TrackedTextHandle SpawnHoldText(string text, Transform targetTransform, Vector3 worldOffset = default, Camera worldCamera = null)
    {
        if (targetCanvas == null || floatingTextPrefab == null || targetTransform == null) return null;

        GameObject sourceObj = targetTransform.gameObject;

        // Clean up pre-existing hold text if active
        if (_activeHoldTexts.TryGetValue(sourceObj, out var existingHandle) && existingHandle != null)
        {
            ReleaseText(existingHandle);
        }

        // 1. Instantiate under target canvas
        GameObject textObj = Instantiate(floatingTextPrefab, targetCanvas.transform);
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        TextMeshProUGUI tmpText = textObj.GetComponentInChildren<TextMeshProUGUI>();

        if (tmpText != null)
        {
            tmpText.text = text;
        }

        // 2. Attach tracking handle
        TrackedTextHandle handle = textObj.AddComponent<TrackedTextHandle>();
        handle.Initialize(rectTransform, tmpText, targetTransform, worldOffset, targetCanvas, worldCamera, this);

        // Track active hold text for this target object
        _activeHoldTexts[sourceObj] = handle;

        // 3. Initial Position calculation
        handle.UpdateCanvasPosition();

        // 4. Punch scale in animation
        rectTransform.DOPunchScale(punchScale, punchDuration, vibrato: 5, elasticity: 0.5f);

        return handle;
    }

    public void ReleaseText(TrackedTextHandle handle)
    {
        if (handle == null) return;

        if (handle.TargetTransform != null)
        {
            GameObject sourceObj = handle.TargetTransform.gameObject;
            if (_activeHoldTexts.TryGetValue(sourceObj, out var currentHandle) && currentHandle == handle)
            {
                _activeHoldTexts.Remove(sourceObj);
            }
        }

        handle.StopTracking();

        RectTransform rectTransform = handle.RectTransform;
        TextMeshProUGUI tmpText = handle.TmpText;

        if (rectTransform == null)
        {
            if (handle.gameObject != null) Destroy(handle.gameObject);
            return;
        }

        rectTransform.DOKill();
        if (tmpText != null) tmpText.DOKill();

        Sequence seq = DOTween.Sequence();

        seq.Append(rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + floatDistance, floatDuration)
            .SetEase(floatEase));

        if (tmpText != null)
        {
            seq.Join(tmpText.DOFade(0f, floatDuration).SetEase(Ease.InQuad));
        }

        seq.OnComplete(() =>
        {
            if (handle != null && handle.gameObject != null)
            {
                Destroy(handle.gameObject);
            }
        });
        if (handle.TargetTransform != null)
        {
            GameObject sourceObj = handle.TargetTransform.gameObject;
            if (_activeHoldTexts.TryGetValue(sourceObj, out var currentHandle) && currentHandle == handle)
            {
                _activeHoldTexts.Remove(sourceObj);
            }
            
            // Stop any stale coroutine tied to this source object
            if (_processingRoutines.TryGetValue(sourceObj, out Coroutine routine))
            {
                if (routine != null) StopCoroutine(routine);
                _processingRoutines.Remove(sourceObj);
                _objectQueues.Remove(sourceObj);
            }
        }
        
    }

    private IEnumerator ProcessObjectQueueRoutine(GameObject source)
    {
        while (_objectQueues.TryGetValue(source, out Queue<SpawnRequest> queue) && queue.Count > 0)
        {
            if (source == null) break;

            SpawnRequest request = queue.Dequeue();
            CreateAndAnimateText(request);

            if (queue.Count > 0 && delayBetweenSpawns > 0f)
            {
                yield return new WaitForSeconds(delayBetweenSpawns);
            }
        }

        // Force removal from dictionaries when loop exits for any reason
        if (source != null)
        {
            _objectQueues.Remove(source);
            _processingRoutines.Remove(source);
        }
        else
        {
            // Cleanup null key references leftover from destroyed objects
            List<GameObject> nullKeys = new List<GameObject>();
            foreach (var key in _processingRoutines.Keys)
            {
                if (key == null) nullKeys.Add(key);
            }
            foreach (var nullKey in nullKeys)
            {
                _objectQueues.Remove(nullKey);
                _processingRoutines.Remove(nullKey);
            }
        }
    }

    private void CreateAndAnimateText(SpawnRequest request)
    {
        if (request.TargetCanvas == null || floatingTextPrefab == null) return;

        GameObject textObj = Instantiate(floatingTextPrefab, request.TargetCanvas.transform);
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        TextMeshProUGUI tmpText = textObj.GetComponentInChildren<TextMeshProUGUI>();

        if (tmpText != null)
        {
            tmpText.text = request.Text;
        }

        Camera cam = request.WorldCamera != null ? request.WorldCamera : Camera.main;
        Vector2 baseCanvasPos = WorldToCanvasPosition(request.TargetCanvas, rectTransform, request.WorldPosition, cam);

        float xDirection;

        // Safely check for destroyed Unity references in dictionary
        if (request.Source != null && 
            _activeHoldTexts.TryGetValue(request.Source, out TrackedTextHandle activeHold) && 
            activeHold != null && 
            activeHold.gameObject != null && 
            activeHold.RectTransform != null)
        {
            float holdX = activeHold.RectTransform.anchoredPosition.x;
            xDirection = holdX >= baseCanvasPos.x ? -1f : 1f;
        }
        else
        {
            // Clean destroyed reference if present
            if (request.Source != null)
            {
                _activeHoldTexts.Remove(request.Source);
            }
            xDirection = baseCanvasPos.x < 0 ? -1f : 1f;
        }

        Vector2 offsetVector = new Vector2(horizontalOffset * xDirection, verticalOffset);
        Vector2 targetCanvasPos = baseCanvasPos + offsetVector;

        rectTransform.anchoredPosition = targetCanvasPos;

        Sequence seq = DOTween.Sequence().SetTarget(textObj);

        seq.Append(rectTransform.DOPunchScale(punchScale, punchDuration, vibrato: 5, elasticity: 0.5f));
        seq.Append(rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + floatDistance, floatDuration)
            .SetEase(floatEase));

        if (tmpText != null)
        {
            seq.Join(tmpText.DOFade(0f, floatDuration).SetEase(Ease.InQuad));
        }

        seq.OnComplete(() =>
        {
            if (textObj != null) Destroy(textObj);
        });
    }

    public Vector2 WorldToCanvasPosition(Canvas canvas, RectTransform element, Vector3 worldPosition, Camera camera)
    {
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector2 screenPoint = camera != null ? camera.WorldToScreenPoint(worldPosition) : (Vector2)worldPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);
            return localPoint;
        }
        else
        {
            Vector2 screenPoint = camera != null ? camera.WorldToScreenPoint(worldPosition) : Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvas.worldCamera, out Vector2 localPoint);
            return localPoint;
        }
    }
}