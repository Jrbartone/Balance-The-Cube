using UnityEngine;
using DG.Tweening;

public class Cube : MonoBehaviour
{
    public GameObject renderedCube;
    public ParticleSystem impactParticles;

    [Header("Audio Configurations")]
    [SerializeField] private AudioCueSO defaultBonkSound;
    [SerializeField] private AudioCueSO defaultBinkSound;
    [SerializeField] private AudioCueSO defaultSlideSound;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transform.rotation = Random.rotation;
    }

    void OnCollisionEnter(Collision collision){
        renderedCube.transform.DOKill(true);
        renderedCube.transform.localScale = Vector3.one;
        renderedCube.transform.DOPunchScale(
            /*Strength */ (Vector3.one + Random.insideUnitSphere * 0.3f) 
                * Mathf.Clamp(collision.relativeVelocity.magnitude * 0.05f, 0.05f, 0.3f),
                /*Duration */ 0.35f,
                /*? */ 10,
                /*? */ 1f).OnComplete(() => transform.localScale = Vector3.one);
        

        playImpactParticles(collision);
    }

    float minImpactForce = 40f; // Force threshold
    float debounceCooldown = 0.4f;
    private float nextAllowedImpactTime;
    void playImpactParticles(Collision collision){
        if (Time.time < nextAllowedImpactTime) return;
        if (impactParticles == null || collision.contacts.Length == 0) return;

        // Calculate impact force magnitude from impulse (scaled by fixed DeltaTime)
        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;

        // Only proceed if force exceeds threshold
        if (impactForce < minImpactForce) {
            AudioManager.Instance.Play3DSFX(defaultBonkSound, transform.position);
            return;
        }
        AudioManager.Instance.Play3DSFX(defaultBinkSound, transform.position);

        // 1. Get the primary contact point and normal
        ContactPoint contact = collision.contacts[0];
        
        // The normal points outward from the surface hit
        Vector3 reflectDirection = contact.normal; 

        // 3. Update debounce timestamp
        nextAllowedImpactTime = Time.time + debounceCooldown;

        // 2. Position the particle system at the point of impact
        impactParticles.transform.position = contact.point;

        // 3. Align local +Y to the normal, with local +Z pointing up
        //impactParticles.transform.rotation = Quaternion.LookRotation(Vector3.up, reflectDirection);

        // 4. Emit the particles
        impactParticles.Play();
    }
}
