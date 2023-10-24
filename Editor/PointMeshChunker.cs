using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace StoryLabResearch.PointCloud
{
    public class PointMeshChunker
    {
        private const float DEFAULT_CHUNK_SIZE = 10f;

        private class Chunk
        {
            private Bounds _bounds;
            private List<Vector3> _vertices;
            private List<Color32> _colors32;

            public Chunk(Vector3 centre, float size)
            {
                _bounds = new(centre, size * Vector3.one);
                _vertices = new();
                _colors32 = new();
            }

            public void AddVertex(Vector3 vertex, Color32 color32)
            {
                _vertices.Add(vertex);
                _colors32.Add(color32);
            }

            public Mesh CreateMesh(string name)
            {
                Mesh mesh = new();
                mesh.indexFormat = _vertices.Count > 65536 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(_vertices);
                mesh.SetColors(_colors32);
                int[] indices = new int[_vertices.Count];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
                mesh.SetIndices(indices, MeshTopology.Points, 0);
                mesh.bounds = _bounds;
                mesh.name = name;
                return mesh;
            }
        }

        private class ChunkCoordinateComparer : IComparer<Vector3Int>
        {
            public int Compare(Vector3Int x, Vector3Int y)
            {
                // we will sort alphabetically
                return x.ToString().CompareTo(y.ToString());
            }
        }

        private class ChunkGroup
        {
            private string _name;
            private Dictionary<Vector3Int, Chunk> _chunks;
            private float _chunkSize = DEFAULT_CHUNK_SIZE;
            private Vector3 _chunkGroupOrigin = Vector3.zero;

            public int Count => _chunks.Count;

            public ChunkGroup(string name, Vector3 chunkGroupOrigin, float chunkSize)
            {
                _name = name;
                //_chunks = new(new ChunkCoordinateComparer());
                _chunks = new();
                _chunkSize = chunkSize;
                _chunkGroupOrigin = chunkGroupOrigin;
            }

            public void AddVertices(Vector3[] vertices, Color32[] colors32)
            {
                if (vertices.Length != colors32.Length)
                {
                    Debug.LogWarning("Vertex count does not equal color count");
                    colors32 = new Color32[vertices.Length];
                }
                for (int i = 0; i < vertices.Length; i++)
                {
                    AddVertex(vertices[i], colors32[i]);
                }
            }

            public void AddVertex(Vector3 vertex, Color32 color32)
            {
                Chunk chunk = GetOrAddChunk(vertex);
                chunk.AddVertex(vertex, color32);
            }

            public Mesh[] CreateMeshes()
            {
                List<Mesh> meshes = new();
                foreach (var element in _chunks)
                {
                    meshes.Add(element.Value.CreateMesh(_name + "_" + element.Key));
                }

                return meshes.ToArray();
            }

            public void SortChunks()
            {
                var sortedChunks = _chunks.OrderBy(i => i.Key.ToString());
                _chunks = new Dictionary<Vector3Int, Chunk>();
                foreach (var kvp in sortedChunks) _chunks.Add(kvp.Key, kvp.Value);
            }

            private Chunk GetOrAddChunk(Vector3 vertex)
            {
                Vector3Int posChunkCoordinates = GetChunkCoordinates(vertex);

                if (_chunks.ContainsKey(posChunkCoordinates))
                {
                    return _chunks[posChunkCoordinates];
                }
                else
                {
                    var chunkCentre = (_chunkSize * (Vector3)posChunkCoordinates) + _chunkGroupOrigin;
                    var chunk = new Chunk(chunkCentre, _chunkSize);
                    _chunks.Add(posChunkCoordinates, chunk);
                    Debug.Log("Added chunk for vertex " + vertex + " at " + posChunkCoordinates);
                    return chunk;
                }
            }

            private Vector3Int GetChunkCoordinates(Vector3 pos)
            {
                var offsetPos = pos - _chunkGroupOrigin;
                var scaledPos = offsetPos / _chunkSize;
                return Vector3Int.RoundToInt(scaledPos);
            }
        }

        public static Mesh[] ChunkPointMesh(Mesh mesh, Vector3 chunkCentre, float chunkSize)
        {
            var chunkGroup = new ChunkGroup(mesh.name, chunkCentre, chunkSize);
            chunkGroup.AddVertices(mesh.vertices, mesh.colors32);
            Debug.Log("Chunk count: " + chunkGroup.Count);

            chunkGroup.SortChunks();

            return chunkGroup.CreateMeshes();
        }
    }
}