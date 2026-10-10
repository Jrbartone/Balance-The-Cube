using UnityEngine;

public abstract class CubeModifier : ScriptableObject
{
    [Header("Base Physics Properties (-7 to 7)")]
    [Range(-7, 7)] public int weight = 0;
    [Range(-7, 7)] public int staticFriction = 0;
    [Range(-7, 7)] public int dynamicFriction = 0;
    [Range(-7, 7)] public int bounciness = 0;
    [Range(-7, 7)] public int size = 0;
    [Range(-7, 7)] public int multi = 0;

    [Header("Physics Field Properties (-7 to 7)")]
    [Range(-7, 7)] public int handAttraction = 0;
    [Range(-7, 7)] public int objectAttraction = 0;
    [Range(-7, 7)] public int centerAttraction = 0;
    [Range(-7, 7)] public int fieldSize = 0;

    [Header("Hand Damage Properties")]
    public bool willDamageHand = false;
    [Range(-7, 7)] public int handDamageAmount = 0;

    [Header("Scoring Properties")]
    public int pointsPerSlideSecond = 0;
    public int pointsPerHeightUnit = 0;
    public int pointsPerStillnessSecond = 0;
    public int pointsPerAirSecond = 0;
    public int pointsPerImpact = 0;

    [Header("Can stack?")]
    public bool canStack = true;

    [Header("Render Mask")]
    public bool disableRenderMask = false;

    [Header("Audio Overrides")]
    public AudioCueSO bonkSoundOverride;
    public AudioCueSO binkSoundOverride;
    public AudioCueSO slideSoundOverride;
    public int soundOverridePriority = 0;

    private void OnValidate()
    {
        // Base Physics Properties
        weight = Mathf.Clamp(weight, -7, 7);
        staticFriction = Mathf.Clamp(staticFriction, -7, 7);
        dynamicFriction = Mathf.Clamp(dynamicFriction, -7, 7);
        bounciness = Mathf.Clamp(bounciness, -7, 7);
        size = Mathf.Clamp(size, -7, 7);
        multi = Mathf.Clamp(multi, -7, 7);

        // Physics Field Properties
        handAttraction = Mathf.Clamp(handAttraction, -7, 7);
        objectAttraction = Mathf.Clamp(objectAttraction, -7, 7);
        centerAttraction = Mathf.Clamp(centerAttraction, -7, 7);
        fieldSize = Mathf.Clamp(fieldSize, -7, 7);

        // Hand Damage Properties
        handDamageAmount = Mathf.Clamp(handDamageAmount, -7, 7);
    }
}