using UnityEngine;

public class MakeCubeReadable : MonoBehaviour
{
    void Awake()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            // Instantiate creates a duplicate copy of the mesh in memory.
            // By default, dynamically generated/instantiated meshes have Read/Write enabled.
            Mesh readableMesh = Instantiate(meshFilter.sharedMesh);
            
            // Assign the readable duplicate back to the filter
            meshFilter.mesh = readableMesh; 
            
            Debug.Log("Cube mesh is now Read/Write enabled!");
        }
    }
}