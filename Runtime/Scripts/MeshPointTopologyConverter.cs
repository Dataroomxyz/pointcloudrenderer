// Adapted from a script by StackExchange user Croolsby, September 6 2015
// https://gamedev.stackexchange.com/questions/97009/geometry-shader-not-generating-geometry-for-some-vertices

using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class MeshPointTopologyConverter : MonoBehaviour
{
    MeshFilter mf;
    Mesh sharedMesh;

    void Update()
    {
        if (mf == null) mf = GetComponent<MeshFilter>();
        if (mf.sharedMesh == sharedMesh) return;

        Mesh mesh = Instantiate(mf.sharedMesh);
        int[] indices = new int[mesh.vertices.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        mesh.SetIndices(indices, MeshTopology.Points, 0);
        mf.sharedMesh = mesh;
        sharedMesh = mesh;
    }
}