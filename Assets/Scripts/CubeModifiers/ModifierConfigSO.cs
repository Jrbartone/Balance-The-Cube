using UnityEngine;

[CreateAssetMenu(fileName = "ModifierConfig", menuName = "Config/ModifierConfig")]
public class ModifierConfigSO : ScriptableObject
{
    [SerializeField] private GameObject iceSlimePrefab;
    public GameObject IceSlimePrefab => iceSlimePrefab;
}