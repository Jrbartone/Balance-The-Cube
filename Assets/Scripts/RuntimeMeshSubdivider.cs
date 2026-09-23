using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RuntimeMeshSubdivider : MonoBehaviour
{
    [Range(0, 5)]
    [Tooltip("Number of subdivision iterations. Warning: Level 5 creates massive geometry!")]
    public int subdivisionLevel = 2;

    private Mesh originalMesh;
    private MeshFilter meshFilter;

    // Cache dictionaries to prevent creating duplicate vertices on shared edges
    private Dictionary<ulong, int> newVerticesCache;
    private List<Vector3> currentVertices;
    private List<Vector3> currentNormals;
    private List<Vector2> currentUVs;

     void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter.sharedMesh != null)
        {
            // Cache the original base cube so we can re-subdivide dynamically if needed
            originalMesh = meshFilter.sharedMesh;
            GenerateSubdividedMesh();
        }
    }

    // Called automatically in the editor when you change the slider inspector value
    void OnValidate()
    {
        if (Application.isPlaying && originalMesh != null && meshFilter != null)
        {
            GenerateSubdividedMesh();
        }
    }

    public void GenerateSubdividedMesh()
    {
        // 1. Always start from a fresh duplicate of the base mesh
        Mesh workingMesh = Instantiate(originalMesh);
        
        // 2. Iteratively subdivide the mesh based on the slider setting
        for (int i = 0; i < subdivisionLevel; i++)
        {
            workingMesh = SubdivideLinear(workingMesh);
        }

        // 3. Assign the newly dense mesh to your filter
        meshFilter.mesh = workingMesh;
    }

    private Mesh SubdivideLinear(Mesh sourceMesh)
    {
        // Initialize working collections
        newVerticesCache = new Dictionary<ulong, int>();
        currentVertices = new List<Vector3>(sourceMesh.vertices);
        currentNormals = new List<Vector3>(sourceMesh.normals);
        currentUVs = new List<Vector2>(sourceMesh.uv);

        int[] oldTriangles = sourceMesh.triangles;
        List<int> newTriangles = new List<int>();

        // Loop through triangles 3 indices at a time
        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int v0 = oldTriangles[i];
            int v1 = oldTriangles[i + 1];
            int v2 = oldTriangles[i + 2];

            // Get or create midpoints for all 3 edges of the triangle
            int a = GetOrCreateMidpointVertex(v0, v1);
            int b = GetOrCreateMidpointVertex(v1, v2);
            int c = GetOrCreateMidpointVertex(v2, v0);

            // Subdivide 1 triangle into 4 smaller, correctly wound triangles
            newTriangles.AddRange(new int[] { v0, a, c });
            newTriangles.AddRange(new int[] { a, v1, b });
            newTriangles.AddRange(new int[] { c, b, v2 });
            newTriangles.AddRange(new int[] { a, b, c }); // Center triangle
        }

        // Rebuild and return the denser mesh
        Mesh subdividedMesh = new Mesh();
        subdividedMesh.indexFormat = (currentVertices.Count > 65535) ? 
            UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

        subdividedMesh.vertices = currentVertices.ToArray();
        subdividedMesh.triangles = newTriangles.ToArray();
        
        if (currentNormals.Count > 0) subdividedMesh.normals = currentNormals.ToArray();
        if (currentUVs.Count > 0) subdividedMesh.uv = currentUVs.ToArray();

        subdividedMesh.RecalculateBounds();
        subdividedMesh.RecalculateTangents();
        
        return subdividedMesh;
    }

    private int GetOrCreateMidpointVertex(int index1, int index2)
    {
        // Generate a unique 64-bit key representing the edge between both indices
        ulong low = (ulong)Mathf.Min(index1, index2);
        ulong high = (ulong)Mathf.Max(index1, index2);
        ulong edgeKey = (low << 32) | high;

        // If this edge midpoint was already created by an adjacent triangle, reuse it!
        if (newVerticesCache.TryGetValue(edgeKey, out int existingIndex))
        {
            return existingIndex;
        }

        // Otherwise, calculate the new midpoints precisely
        int newIndex = currentVertices.Count;
        
        Vector3 midVertex = (currentVertices[index1] + currentVertices[index2]) * 0.5f;
        currentVertices.Add(midVertex);

        if (currentNormals.Count > 0)
        {
            Vector3 midNormal = (currentNormals[index1] + currentNormals[index2]).normalized;
            currentNormals.Add(midNormal);
        }

        if (currentUVs.Count > 0)
        {
            Vector2 midUV = (currentUVs[index1] + currentUVs[index2]) * 0.5f;
            currentUVs.Add(midUV);
        }

        newVerticesCache.Add(edgeKey, newIndex);
        return newIndex;
    }
}
