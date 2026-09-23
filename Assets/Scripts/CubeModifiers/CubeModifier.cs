using UnityEngine;

public abstract class CubeModifier : ScriptableObject
{
    [Header("Base Properties (-7 to 7)")]
    [Range(-7, 7)] public int weight = 0;
    [Range(-7, 7)] public int staticFriction = 0;
    [Range(-7, 7)] public int dynamicFriction = 0;
    [Range(-7, 7)] public int bounciness = 0;
    [Range(-7, 7)] public int size = 0;
    [Range(-7, 7)] public int multi = 0;

    public bool disableRenderMask = false;

    private void OnValidate()
    {
        weight = Mathf.Clamp(weight, -7, 7);
        staticFriction = Mathf.Clamp(staticFriction, -7, 7);
        dynamicFriction = Mathf.Clamp(dynamicFriction, -7, 7);
        bounciness = Mathf.Clamp(bounciness, -7, 7);
        size = Mathf.Clamp(size, -7, 7);
        multi = Mathf.Clamp(multi, -7, 7);
    }
}