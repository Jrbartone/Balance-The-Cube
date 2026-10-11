using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class TMPTextShake : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxShakeSpeed = 40f;

    private TMP_Text textComponent;
    
    // Dynamic values driven by ScoreManager
    private float currentStrength = 0f;
    private float currentSpeed = 0f;

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    // Called every frame by ScoreManager to update intensity dynamically
    public void SetShakeConfig(float strength, float speed)
    {
        currentStrength = strength;
        currentSpeed = speed;
    }

    private void LateUpdate()
    {
        if (textComponent == null) return;
        
        // If strength is effectively zero, skip processing to save performance
        if (currentStrength <= 0.001f) return;

        textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = textComponent.textInfo;

        int characterCount = textInfo.characterCount;
        if (characterCount == 0) return;

        for (int i = 0; i < characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;

            Vector3 randomOffset = new Vector3(
                Mathf.Sin(time * currentSpeed + i) * currentStrength,
                Mathf.Cos(time * currentSpeed * 1.3f + i) * currentStrength,
                0f
            );

            for (int j = 0; j < 4; j++)
            {
                vertices[vertexIndex + j] += randomOffset;
            }
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
    }
}