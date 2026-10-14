using System;
using UnityEngine;
using UnityEngine.Playables;

public class CutsceneManager : MonoBehaviour 
{
    // Static singleton instance accessible from any script
    public static CutsceneManager Instance { get; private set; }

    private void Awake()
    {
        // Enforce singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Optional: keeps manager alive between scene loads
    }

    public void PlayCutscene(PlayableDirector director, Action onCutsceneEnded = null)
    {
        if (director == null) return;

        director.Play();

        // Local anonymous function to handle cleanup and execute the custom callback
        Action<PlayableDirector> handler = null;
        handler = (dir) => 
        {
            director.stopped -= handler; // Unsubscribe to prevent memory leaks
            onCutsceneEnded?.Invoke();   // Execute the optional lambda
        };

        director.stopped += handler;
    }
}
