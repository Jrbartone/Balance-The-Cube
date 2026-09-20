using UnityEngine;

[CreateAssetMenu(fileName = "NewCubeMaterialModifier", menuName = "Cube Modifiers/Material Modifier")]
public class CubeMaterialModifer : CubeModifier
{
    [Header("Material")]
    public Material material;

    [Header("Audio Overrides")]
    public AudioCueSO bonkSound;
    public AudioCueSO binkSound;
    public AudioCueSO slideSound;
}