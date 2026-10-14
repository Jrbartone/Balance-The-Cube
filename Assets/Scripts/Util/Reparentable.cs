using UnityEngine;

public class Reparentable : MonoBehaviour
{    
    public void reparent(GameObject newParent)
    {
        transform.parent = newParent.transform;
    }

    public void clearParent()
    {
        transform.parent = null;
    }

    public void reparentAndSetToPosition(GameObject newParent)
    {
        transform.parent = newParent.transform;
        transform.localPosition = Vector3.zero;
    }
}