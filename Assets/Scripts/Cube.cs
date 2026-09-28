using UnityEngine;

public class Cube : MonoBehaviour
{
    private void Start()
    {
        transform.rotation = Random.rotation;
    }
}