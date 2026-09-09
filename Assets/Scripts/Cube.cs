using UnityEngine;
using DG.Tweening;

public class Cube : MonoBehaviour
{
    public GameObject renderedCube;
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
    }
}
