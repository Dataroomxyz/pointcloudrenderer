using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StoryLabResearch.PointCloud
{
    public class PointMeshSubsampler
    {
        public static Mesh SubsampleMesh(Mesh originalMesh, float subsampleFactor, string suffix = "", bool compensateArea = true)
        {
            var originalVertices = originalMesh.vertices;
            var originalColors32 = originalMesh.colors32;
            int step = Mathf.RoundToInt(1 / Mathf.Clamp01(subsampleFactor));
            int outputVertexCount = originalMesh.vertexCount / step;
            float areaCompensation = compensateArea ? 1f / subsampleFactor : 1f;
            byte radiusCompensation = (byte)Mathf.RoundToInt(Mathf.Sqrt(areaCompensation));

            var vertices = new Vector3[outputVertexCount];
            var colors32 = new Color32[outputVertexCount];

            for (int i = 0; i < outputVertexCount; i++)
            {
                vertices[i] = originalVertices[i * step];
                Color32 color32 = originalColors32[i * step];
                colors32[i] = new Color32(color32.r, color32.g, color32.b, radiusCompensation);
            }

            Mesh mesh = new();
            mesh.indexFormat = vertices.Length > 65536 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors32);
            int[] indices = new int[vertices.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetIndices(indices, MeshTopology.Points, 0);
            mesh.bounds = originalMesh.bounds;
            mesh.name = originalMesh.name + suffix;

            return mesh;
        }
    }
}