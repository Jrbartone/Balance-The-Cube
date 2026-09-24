using UnityEngine;

[CreateAssetMenu(fileName = "NewCubeEffectModifier", menuName = "Cube Modifiers/Effect Modifier")]
public class CubeEffectModifier : CubeModifier
{
    [Header("Visual Effects")]
    [Tooltip("Prefab representing a visual update or overlay on the Cube.")]
    public GameObject visualEffectPrefab;
    public GameObject impactParticlePrefab;
    public bool disableImpactParticles = false;
    public int impactParticlePriority = 0;

    [Header("Sound Effects")]
    public AudioCueSO loopingSoundEffect;
}