using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class DeviceDetector : MonoBehaviour
{
    // Static singleton reference accessible from anywhere
    public static DeviceDetector Instance { get; private set; }

    // Public state properties
    public bool IsGamepad { get; private set; }

    // Event you can subscribe to from other scripts (e.g., UI Managers)
    public static event Action<bool> OnDeviceChanged;

    private void Awake()
    {
        // Enforce the Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        // Optional: Keep this object alive across scene transitions
        DontDestroyOnLoad(gameObject); 
    }

    private void OnEnable()
    {
        InputSystem.onActionChange += OnActionChange;
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= OnActionChange;
    }

    private void OnActionChange(object obj, InputActionChange change)
    {
        // Only trigger when an actual button press or joystick move happens
        if (change == InputActionChange.ActionPerformed && obj is InputAction action)
        {
            InputDevice lastDevice = action.activeControl.device;
            bool wasGamepad = IsGamepad;

            // Update the state
            IsGamepad = lastDevice is Gamepad;

            // Only fire the event if the device type actually swapped
            if (wasGamepad != IsGamepad)
            {
//                Debug.Log($"Input device changed. Is Gamepad: {IsGamepad}");
                OnDeviceChanged?.Invoke(IsGamepad);
            }
        }
    }
}
