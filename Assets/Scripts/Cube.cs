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
    float minAngularVelocityForSlide = 2f;
    int collidedObjects = 0;
    TrackedAudioInstance slideAudioInstance;

    private float targetSlideVolume = 0f;
    private float currentSlideVolume = 0f;

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
        renderedCube.transform.DOKill(true);
        renderedCube.transform.localScale = Vector3.one;
        renderedCube.transform.DOPunchScale(
            /*Strength */ (Vector3.one + Random.insideUnitSphere * 0.3f) 
                * Mathf.Clamp(collision.relativeVelocity.magnitude * 0.05f, 0.05f, 0.3f),
            /*Duration */ 0.35f,
            /*Vibrato */ 10,
            /*Elasticity */ 1f).OnComplete(() => transform.localScale = Vector3.one);

        playImpactParticles(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        if(collidedObjects == 0){
            collidedObjects = 1;
        }
        // Determine intended target volume based on velocity and state
        if (rb.linearVelocity.magnitude <= minSlideVelocity || rb.angularVelocity.magnitude >= minAngularVelocityForSlide)
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

    float minImpactForce = 40f; // Force threshold
    float debounceCooldown = 0.4f;
    private float nextAllowedImpactTime;

    void playImpactParticles(Collision collision)
    {
        if (Time.time < nextAllowedImpactTime) return;
        if (impactParticles == null || collision.contacts.Length == 0) return;

        // Calculate impact force magnitude from impulse (scaled by fixed DeltaTime)
        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;

        // Only proceed if force exceeds threshold
        if (impactForce < minImpactForce) 
        {
            AudioManager.Instance.Play3DSFX(defaultBonkSound, transform.position);
            return;
        }
        AudioManager.Instance.Play3DSFX(defaultBinkSound, transform.position);

        // Get the primary contact point and normal
        ContactPoint contact = collision.contacts[0];
        
        // Update debounce timestamp
        nextAllowedImpactTime = Time.time + debounceCooldown;

        // Position the particle system at the point of impact
        impactParticles.transform.position = contact.point;

        // Emit the particles
        impactParticles.Play();
    }
}