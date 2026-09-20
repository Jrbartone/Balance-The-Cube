using UnityEngine;
using DG.Tweening;
using UnityEngine.Pool;
using System.Collections;

public class Cube : MonoBehaviour
{
    public GameObject renderedCube;
    public ParticleSystem impactParticles;

    [Header("Audio Configurations")]
    [SerializeField] private AudioCueSO defaultBonkSound;
    [SerializeField] private AudioCueSO defaultBinkSound;
    [SerializeField] private AudioCueSO defaultSlideSound;

    [Header("Slide Audio Smoothing")]
    [SerializeField] private float audioFadeSpeed = 5f; // Adjust to speed up or slow down the fade duration

    private Rigidbody rb;
    float maxSlideVelocity = 5f;
    float minSlideVelocity = .005f;
    int collidedObjects = 0;
    TrackedAudioInstance slideAudioInstance;

    private float targetSlideVolume = 0f;
    private float currentSlideVolume = 0f;

    [Header("Impact FX tuning")]
    [SerializeField] private float minImpactForce = 20f; // Force threshold
    float debounceCooldown = 0.4f;
    private float nextAllowedImpactTime;

    void Start()
    {
        transform.rotation = Random.rotation;
        rb = gameObject.GetComponent<Rigidbody>();
        slideAudioInstance = AudioManager.Instance.PlayTrackedLoop(defaultSlideSound, transform);
    }

    void Update()
    {
        // Smoothly update the actual audio volume toward the calculated target each frame
        if (currentSlideVolume != targetSlideVolume)
        {
            currentSlideVolume = Mathf.MoveTowards(currentSlideVolume, targetSlideVolume, audioFadeSpeed * Time.deltaTime);
            slideAudioInstance.UpdateParameters(currentSlideVolume, 1f);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        collidedObjects++;
        // Only proceed if force exceeds threshold
        if (collision.impulse.magnitude / Time.fixedDeltaTime > minImpactForce) 
        {
            onImpact(collision);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if(collidedObjects == 0){
            collidedObjects = 1;
        }
        // Determine intended target volume based on velocity and state
        if (rb.linearVelocity.magnitude <= minSlideVelocity)
        {
            targetSlideVolume = 0f;
        }
        else
        {
            targetSlideVolume = Mathf.Clamp01(rb.linearVelocity.magnitude / maxSlideVelocity);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        collidedObjects = Mathf.Max(0, collidedObjects - 1);
        if (collidedObjects == 0)
        {
            targetSlideVolume = 0f;
        }
    }

    void onImpact(Collision collision)
    {
        if (Time.time < nextAllowedImpactTime) return;    
        // Update debounce timestamp
        nextAllowedImpactTime = Time.time + debounceCooldown;
        playImpactParticles(collision);
        playImpactSounds(collision);
        playImpactTween(collision);
    }

    void playImpactTween(Collision collision){

        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;

        // Only proceed if force exceeds threshold
        if (impactForce < minImpactForceForBink) 
        {
            renderedCube.transform.DOKill(true);
            renderedCube.transform.localScale = Vector3.one;
            renderedCube.transform.DOPunchScale(
            /*Strength */ (Vector3.one + Random.insideUnitSphere * 0.1f) 
                * Mathf.Clamp(collision.relativeVelocity.magnitude * 0.05f, 0.05f, 0.3f),
            /*Duration */ 0.35f,
            /*Vibrato */ 10,
            /*Elasticity */ 1f).OnComplete(() => transform.localScale = Vector3.one);
           return;
        }

        renderedCube.transform.DOKill(true);
        renderedCube.transform.localScale = Vector3.one;
        renderedCube.transform.DOPunchScale(
            /*Strength */ (Vector3.one + Random.insideUnitSphere * 0.3f) 
                * Mathf.Clamp(collision.relativeVelocity.magnitude * 0.05f, 0.05f, 0.3f),
            /*Duration */ 0.35f,
            /*Vibrato */ 10,
            /*Elasticity */ 1f).OnComplete(() => transform.localScale = Vector3.one);

    }

    void playImpactParticles(Collision collision){
        if (impactParticles == null || collision.contacts.Length == 0) return;

        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;

        // Only proceed if force exceeds threshold
        if (impactForce < minImpactForceForBink) 
        {
           return;
        }

        // Get the primary contact point and normal
        ContactPoint contact = collision.contacts[0];
        
        // Position the particle system at the point of impact
        impactParticles.transform.position = contact.point;

        // Emit the particles
        impactParticles.Play();

    }

    float minImpactForceForBink = 175f; // Force threshold
    void playImpactSounds(Collision collision){
          // Calculate impact force magnitude from impulse (scaled by fixed DeltaTime)
        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;

        // Only proceed if force exceeds threshold
        if (impactForce > minImpactForceForBink) 
        {
            AudioManager.Instance.Play3DSFX(defaultBinkSound, transform.position);
        } else {
            AudioManager.Instance.Play3DSFX(defaultBonkSound, transform.position);
        }
    }
}