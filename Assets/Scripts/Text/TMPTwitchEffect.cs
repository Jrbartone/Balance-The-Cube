using System.Collections;
using UnityEngine;
using TMPro;

public class TMPSTwitchEffect : MonoBehaviour
{
    private TMP_Text textComponent;

    [Header("Position Twitch")]
    [Tooltip("Maximum pixel distance a letter can jump away from its resting spot.")]
    [SerializeField] private float twitchIntensity = 1.5f;

    [Header("Rotation Twitch")]
    [Tooltip("Maximum degrees a letter can rotate clockwise or counter-clockwise.")]
    [SerializeField] private float maxRotationAngle = 5.0f;

    [Header("Timing")]
    [Tooltip("Minimum time (in seconds) a character waits before twitching.")]
    [SerializeField] private float minWaitTime = 1.0f;

    [Tooltip("Maximum time (in seconds) a character waits before twitching.")]
    [SerializeField] private float maxWaitTime = 4.0f;

    [Tooltip("How long a single twitch displacement and rotation lasts before returning to rest.")]
    [SerializeField] private float twitchDuration = 0.08f;

    // Tracks individual timers, position offsets, and rotation angles for each character index
    private float[] nextTwitchTime;
    private Vector3[] characterOffsets;
    private float[] characterRotations;

    void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    void Start()
    {
        InitializeTimers();
    }

    void OnEnable()
    {
        InitializeTimers();
    }

    void InitializeTimers()
    {
        textComponent.ForceMeshUpdate();
        int charCount = textComponent.textInfo.characterCount;
        
        nextTwitchTime = new float[charCount];
        characterOffsets = new Vector3[charCount];
        characterRotations = new float[charCount];

        // Stagger the initial twitch times so they don't all trigger at once
        for (int i = 0; i < charCount; i++)
        {
            nextTwitchTime[i] = Time.time + Random.Range(0f, maxWaitTime);
            characterOffsets[i] = Vector3.zero;
            characterRotations[i] = 0f;
        }
    }

    void Update()
    {
        TMP_TextInfo textInfo = textComponent.textInfo;
        int charCount = textInfo.characterCount;

        // Safety check if text string dynamically changes length
        if (nextTwitchTime == null || nextTwitchTime.Length < charCount)
        {
            InitializeTimers();
            return;
        }

        bool needsMeshUpdate = false;

        for (int i = 0; i < charCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            // Check if it's time for this specific character to twitch
            if (Time.time >= nextTwitchTime[i])
            {
                StartCoroutine(TwitchCharacterRoutine(i));
                nextTwitchTime[i] = Time.time + Random.Range(minWaitTime, maxWaitTime);
            }

            // Apply modifications if the character has an active position offset OR rotation
            if (characterOffsets[i] != Vector3.zero || characterRotations[i] != 0f)
            {
                needsMeshUpdate = true;
                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                Vector3[] destinationVertices = textInfo.meshInfo[materialIndex].vertices;

                // 1. Find the baseline center point of the 4 vertices so the letter rotates around its own center
                Vector3 center = (destinationVertices[vertexIndex + 0] + 
                                  destinationVertices[vertexIndex + 1] + 
                                  destinationVertices[vertexIndex + 2] + 
                                  destinationVertices[vertexIndex + 3]) * 0.25f;

                // 2. Create a rotation matrix based on the random angle [1]
                Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, characterRotations[i]), Vector3.one);

                // 3. Apply rotation (relative to center) and then add the positional twitch offset to all 4 corners [1]
                for (int j = 0; j < 4; j++)
                {
                    Vector3 origVertex = destinationVertices[vertexIndex + j];
                    // Subtract center to move to local origin, rotate, add center back, then add translation offset [1]
                    destinationVertices[vertexIndex + j] = matrix.MultiplyPoint3x4(origVertex - center) + center + characterOffsets[i];
                }
            }
        }

        // Only update geometry if changes were made this frame
        if (needsMeshUpdate)
        {
            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                textComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }
        }
        else
        {
            // Keep the baseline clean when everyone is at rest
            textComponent.ForceMeshUpdate();
        }
    }

    private IEnumerator TwitchCharacterRoutine(int charIndex)
    {
        // Generate random translation offset
        characterOffsets[charIndex] = new Vector3(
            Random.Range(-twitchIntensity, twitchIntensity),
            Random.Range(-twitchIntensity, twitchIntensity),
            0
        );

        // Generate random rotation angle
        characterRotations[charIndex] = Random.Range(-maxRotationAngle, maxRotationAngle);

        // Hold the twitch briefly
        yield return new WaitForSeconds(twitchDuration);

        // Snap completely back to resting position and 0 rotation
        characterOffsets[charIndex] = Vector3.zero;
        characterRotations[charIndex] = 0f;
    }
}
