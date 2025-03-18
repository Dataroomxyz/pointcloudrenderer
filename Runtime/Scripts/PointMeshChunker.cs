using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace StoryLabResearch.PointCloud
{
    public class PointMeshChunker
    {
        private const float DEFAULT_CHUNK_SIZE = 10f;
        private const bool DEFAULT_OPTIMIZE_FOR_MOBILE = false;
        private const bool DEFAULT_USE_BOUNDS_OPTIMIZATION = true;

        private class Chunk
        {
            private Bounds _bounds;
            private List<Vector3> _vertices;
            private List<Color32> _colors32;
            private bool _optimizeForMobile;
            
            public Vector3 Centre => _bounds.center;
            public int VertexCount => _vertices.Count;

            public Chunk(Vector3 centre, float size, bool optimizeForMobile = false)
            {
                _bounds = new Bounds(centre, size * Vector3.one);
                _vertices = new List<Vector3>();
                _colors32 = new List<Color32>();
                _optimizeForMobile = optimizeForMobile;
            }

            public void AddVertex(Vector3 vertex, Color32 color32)
            {
                _vertices.Add(vertex);
                _colors32.Add(color32);
            }

            public Mesh CreateMesh(string name)
            {
                if (_vertices.Count == 0)
                    return null;
                    
                Mesh mesh = new Mesh();
                mesh.indexFormat = _vertices.Count > 65536 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                
                // Mobile optimizations - more aggressive vertex reduction
                if (_optimizeForMobile && _vertices.Count > 10000)
                {
                    // For very large chunks on mobile, do a basic decimation by removing every other vertex
                    int targetCount = Mathf.Min(_vertices.Count, 10000);
                    int stride = Mathf.Max(1, _vertices.Count / targetCount);
                    
                    var optimizedVertices = new List<Vector3>(targetCount);
                    var optimizedColors = new List<Color32>(targetCount);
                    
                    for (int i = 0; i < _vertices.Count; i += stride)
                    {
                        optimizedVertices.Add(_vertices[i]);
                        
                        // Increase alpha (point size) to compensate for lower density
                        Color32 color = _colors32[i];
                        byte adjustedAlpha = (byte)Mathf.Min(255, color.a * Mathf.Sqrt(stride));
                        optimizedColors.Add(new Color32(color.r, color.g, color.b, adjustedAlpha));
                    }
                    
                    mesh.SetVertices(optimizedVertices);
                    mesh.SetColors(optimizedColors);
                    
                    int[] indices = new int[optimizedVertices.Count];
                    for (int i = 0; i < indices.Length; i++) indices[i] = i;
                    mesh.SetIndices(indices, MeshTopology.Points, 0);
                }
                else
                {
                    // Standard approach for non-mobile or smaller chunks
                    mesh.SetVertices(_vertices);
                    mesh.SetColors(_colors32);
                    
                    int[] indices = new int[_vertices.Count];
                    for (int i = 0; i < indices.Length; i++) indices[i] = i;
                    mesh.SetIndices(indices, MeshTopology.Points, 0);
                }
                
                mesh.bounds = _bounds;
                mesh.name = name;
                return mesh;
            }
        }

        private class ChunkCoordinateComparer : IComparer<Vector3Int>
        {
            public int Compare(Vector3Int x, Vector3Int y)
            {
                // Sort by distance from origin to optimize draw order
                int xMag = x.x * x.x + x.y * x.y + x.z * x.z;
                int yMag = y.x * y.x + y.y * y.y + y.z * y.z;
                
                return xMag.CompareTo(yMag);
            }
        }

        private class ChunkGroup
        {
            private string _name;
            private Dictionary<Vector3Int, Chunk> _chunks;
            private float _chunkSize = DEFAULT_CHUNK_SIZE;
            private Vector3 _chunkGroupOrigin = Vector3.zero;
            private bool _optimizeForMobile = DEFAULT_OPTIMIZE_FOR_MOBILE;
            private bool _useBoundsOptimization = DEFAULT_USE_BOUNDS_OPTIMIZATION;

            public int Count => _chunks.Count;
            public int TotalVertexCount
            {
                get
                {
                    int count = 0;
                    foreach (var chunk in _chunks.Values)
                    {
                        count += chunk.VertexCount;
                    }
                    return count;
                }
            }

            public ChunkGroup(string name, Vector3 chunkGroupOrigin, float chunkSize, bool optimizeForMobile = false, bool useBoundsOptimization = true)
            {
                _name = name;
                _chunks = new Dictionary<Vector3Int, Chunk>();
                _chunkSize = chunkSize;
                _chunkGroupOrigin = chunkGroupOrigin;
                _optimizeForMobile = optimizeForMobile;
                _useBoundsOptimization = useBoundsOptimization;
            }

            public void AddVertices(Vector3[] vertices, Color32[] colors32)
            {
                if (vertices.Length != colors32.Length)
                {
                    Debug.LogWarning("Vertex count does not equal color count");
                    colors32 = new Color32[vertices.Length];
                }
                
                // Pre-compute chunk coordinates for better memory allocation
                var chunkCoordinates = new Vector3Int[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    chunkCoordinates[i] = GetChunkCoordinates(vertices[i]);
                }
                
                // First pass: count vertices per chunk to pre-allocate
                var vertexCountPerChunk = new Dictionary<Vector3Int, int>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3Int coord = chunkCoordinates[i];
                    if (!vertexCountPerChunk.ContainsKey(coord))
                    {
                        vertexCountPerChunk[coord] = 0;
                    }
                    vertexCountPerChunk[coord]++;
                }
                
                // Create chunks with appropriate capacity
                foreach (var kvp in vertexCountPerChunk)
                {
                    var chunkCentre = (_chunkSize * (Vector3)kvp.Key) + _chunkGroupOrigin;
                    var chunk = new Chunk(chunkCentre, _chunkSize, _optimizeForMobile);
                    _chunks.Add(kvp.Key, chunk);
                }
                
                // Second pass: add vertices to chunks
                for (int i = 0; i < vertices.Length; i++)
                {
                    _chunks[chunkCoordinates[i]].AddVertex(vertices[i], colors32[i]);
                }
            }

            public void AddVertex(Vector3 vertex, Color32 color32)
            {
                Chunk chunk = GetOrAddChunk(vertex);
                chunk.AddVertex(vertex, color32);
            }

            public Mesh[] CreateMeshes()
            {
                List<Mesh> meshes = new List<Mesh>();
                foreach (var element in _chunks)
                {
                    var mesh = element.Value.CreateMesh(_name + "_" + element.Key);
                    if (mesh != null)
                    {
                        meshes.Add(mesh);
                    }
                }

                return meshes.ToArray();
            }

            public void SortChunks()
            {
                // For VR, sort chunks by distance from origin for better draw ordering
                var sortedChunks = _optimizeForMobile ? 
                    _chunks.OrderBy(i => i.Key, new ChunkCoordinateComparer()) : 
                    _chunks.OrderBy(i => i.Key.ToString());
                    
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
                    var chunk = new Chunk(chunkCentre, _chunkSize, _optimizeForMobile);
                    _chunks.Add(posChunkCoordinates, chunk);
                    return chunk;
                }
            }

            private Vector3Int GetChunkCoordinates(Vector3 pos)
            {
                var offsetPos = pos - _chunkGroupOrigin;
                var scaledPos = offsetPos / _chunkSize;
                return Vector3Int.RoundToInt(scaledPos);
            }
            
            public void OptimizeCameraCulling(Camera camera)
            {
                if (!_useBoundsOptimization || camera == null)
                    return;
                    
                // For VR optimization, identify which chunks are in the camera's frustum
                Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
                
                foreach (var chunk in _chunks.Values)
                {
                    // Create a bounds object for this chunk
                    Bounds chunkBounds = new Bounds(chunk.Centre, Vector3.one * _chunkSize);
                    
                    // Check if this chunk is visible
                    if (!GeometryUtility.TestPlanesAABB(frustumPlanes, chunkBounds))
                    {
                        // TODO: Mark this chunk as not visible for culling
                    }
                }
            }
        }

        public static Mesh[] ChunkPointMesh(Mesh mesh, Vector3 chunkCentre, float chunkSize, bool optimizeForMobile = false, bool useBoundsOptimization = true)
        {
            var chunkGroup = new ChunkGroup(mesh.name, chunkCentre, chunkSize, optimizeForMobile, useBoundsOptimization);
            
            // Use batch vertex addition for better performance
            chunkGroup.AddVertices(mesh.vertices, mesh.colors32);
            
            Debug.Log($"Created {chunkGroup.Count} chunks with {chunkGroup.TotalVertexCount} total vertices from original {mesh.vertexCount} vertices");

            chunkGroup.SortChunks();

            return chunkGroup.CreateMeshes();
        }
        
        public static Mesh[] ChunkPointMeshForVR(Mesh mesh, Vector3 chunkCentre, float chunkSize)
        {
            // Convenience method with VR-optimized defaults
            return ChunkPointMesh(mesh, chunkCentre, chunkSize, true, true);
        }
    }
}
