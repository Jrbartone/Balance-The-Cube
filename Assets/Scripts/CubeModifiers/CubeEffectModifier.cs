using UnityEngine;

[CreateAssetMenu(fileName = "NewCubeEffectModifier", menuName = "Cube Modifiers/Effect Modifier")]
public class CubeEffectModifier : CubeModifier
{
    [Header("Visual Effects")]
    [Tooltip("Prefab representing a visual update or overlay on the Cube.")]
    public GameObject visualEffectPrefab;
}