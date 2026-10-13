using UnityEngine;

public class Cube : MonoBehaviour
{
    private Rigidbody rb;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        rb.rotation = Random.rotation;
    }
}