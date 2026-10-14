using UnityEngine;

public class SetKinematicable : MonoBehaviour
{
    private Rigidbody rb;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }
    public void setKinematic()
    {
        rb.isKinematic = true;
    }

    public void setNonKinematic()
    {
        rb.isKinematic = false;
    }
}