using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace StoryLabResearch.PointCloud
{
    public static class PointMeshSubsampler
    {
        public enum ESubsampleMode
        {
            None,
            SpatialFast,
            SpatialThreePass,
            SpatialExact,
            Random
        }

        private const int FIRST_PASS_NUM_NEIGHBOURS = 1000;
        private const float FIRST_PASS_LENIENCY = 0.3f; // make the first pass more strict than the second, so we don't remove too many vertices on borders (this is a multiplier for square distance)
        private const int SECOND_PASS_NUM_NEIGHBOURS = 8888;
        private const float SECOND_PASS_LENIENCY = 0.7f; // only used if we are not skipping the final pass

        public class Range
        {
            public int Lower;
            public int Upper;

            public Range(int lower, int upper)
            {
                Lower = lower;
                Upper = upper;
            }
        }

        public static Mesh SubsampleMesh(ESubsampleMode mode, Mesh originalMesh, string outputName, float subsampleValue, int referencePointCount = -1, bool compensateArea = true)
        {
            if (subsampleValue < 0f) throw new Exception("Subsample value less than 0, an error has probably occurred");
            return mode switch
            {
                ESubsampleMode.SpatialExact or ESubsampleMode.SpatialThreePass or ESubsampleMode.SpatialFast => SubsampleMeshSpatial(originalMesh, outputName, subsampleValue, mode, referencePointCount, compensateArea),
                ESubsampleMode.Random => SubsampleMeshRandom(originalMesh, outputName, subsampleValue, referencePointCount, compensateArea),
                _ => originalMesh,
            };
        }

        private static Mesh SubsampleMeshRandom(Mesh originalMesh, string outputName, float subsampleFactor, int referencePointCount = - 1, bool compensateArea = true)
        {
            float effectiveSubsampleFactor = subsampleFactor;
            if(referencePointCount > 0) effectiveSubsampleFactor = Mathf.Clamp01(subsampleFactor * referencePointCount / originalMesh.vertexCount);

            if (subsampleFactor >= 1f - Mathf.Epsilon) return originalMesh;

            var originalVertices = originalMesh.vertices;
            var originalColors32 = originalMesh.colors32;
            int step = Mathf.RoundToInt(1 / Mathf.Clamp01(effectiveSubsampleFactor));
            int outputVertexCount = originalMesh.vertexCount / step;
            float areaCompensation = compensateArea ? 1f / subsampleFactor : 1f;
            byte radiusCompensation = (byte)Mathf.RoundToInt(Mathf.Sqrt(areaCompensation));

            var newVertices = new Vector3[outputVertexCount];
            var newColors32 = new Color32[outputVertexCount];

            for (int i = 0; i < outputVertexCount; i++)
            {
                newVertices[i] = originalVertices[i * step];
                Color32 color32 = originalColors32[i * step];
                newColors32[i] = new Color32(color32.r, color32.g, color32.b, radiusCompensation);
            }

            return MeshFromArrays(newVertices, newColors32, outputName, originalMesh.bounds);
        }

        private static Mesh SubsampleMeshSpatial(Mesh originalMesh, string outputName, float subsampleDistance, ESubsampleMode mode = ESubsampleMode.SpatialFast, int referencePointCount = -1, bool compensateArea = true)
        {
            if (Mathf.Abs(subsampleDistance) < Mathf.Epsilon) return originalMesh;

            var originalCount = originalMesh.vertexCount;
            var originalVertices = originalMesh.vertices;
            var originalColors32 = originalMesh.colors32;

            var invalids = new bool[originalMesh.vertexCount];

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Mathf.Max(1, Environment.ProcessorCount - 2)
            };
            Debug.Log("Max Degree of Parallelism: " + parallelOptions.MaxDegreeOfParallelism);

            Debug.Log("Initial vertex count: " + originalCount);

            bool prePasses = false;
            bool finalPass = false;

            switch(mode)
            {
                case ESubsampleMode.SpatialFast:
                    prePasses = true;
                    finalPass = false;
                    break;
                case ESubsampleMode.SpatialThreePass:
                    prePasses = true;
                    finalPass = true;
                    break;
                case ESubsampleMode.SpatialExact:
                    prePasses = false;
                    finalPass = true;
                    break;
            }

            // PRE PASSES

            if (prePasses)
            {
                // FIRST PASS

                int threads = Mathf.CeilToInt((float)originalCount / FIRST_PASS_NUM_NEIGHBOURS);
                var threadRanges = new Range[threads];
                for (int i = 0; i < threads; i++)
                {
                    int offset = i * FIRST_PASS_NUM_NEIGHBOURS;
                    threadRanges[i] = new Range(offset, Mathf.Min(offset + FIRST_PASS_NUM_NEIGHBOURS - 1, originalCount - 1));
                }

                float squareDistanceLimit = subsampleDistance * subsampleDistance * FIRST_PASS_LENIENCY;
                Parallel.ForEach(threadRanges, parallelOptions, range =>
                {
                    MarkInvalidVerticesSpatial(originalVertices, invalids, squareDistanceLimit, range);
                });

                var interimCount = originalCount - SumBools(invalids);
                Debug.Log("First pass vertex count: " + interimCount);

                // SECOND PASS

                threads = Mathf.CeilToInt((float)originalCount / SECOND_PASS_NUM_NEIGHBOURS);
                threadRanges = new Range[threads];
                for (int i = 0; i < threads; i++)
                {
                    int offset = i * SECOND_PASS_NUM_NEIGHBOURS;
                    threadRanges[i] = new Range(offset, Mathf.Min(offset + SECOND_PASS_NUM_NEIGHBOURS - 1, originalCount - 1));
                }

                // use the final distance limit if we are skipping the final pass
                squareDistanceLimit = subsampleDistance * subsampleDistance * (finalPass ? SECOND_PASS_LENIENCY : 1f);
                Parallel.ForEach(threadRanges, parallelOptions, range =>
                {
                    MarkInvalidVerticesSpatial(originalVertices, invalids, squareDistanceLimit, range);
                });

                interimCount = originalCount - SumBools(invalids);
                Debug.Log("Second pass vertex count: " + interimCount);
            }
            else
            {
                Debug.Log("Skipping pre passes");
            }

            // FINAL PASS

            if (finalPass)
            {
                float squareDistanceLimit = subsampleDistance * subsampleDistance;
                MarkInvalidVerticesSpatial(originalVertices, invalids, squareDistanceLimit);
            }
            else
            {
                Debug.Log("Skipping final pass");
            }

            var newCount = originalCount - SumBools(invalids);
            Debug.Log("Final vertex count: " + newCount);

            var newVertices = new Vector3[newCount];
            var newColors32 = new Color32[newCount];

            var subsampleReferenceCount = referencePointCount > 0 ? referencePointCount : originalCount;
            var effectiveSubsampleFactor = (float)newCount / subsampleReferenceCount;
            var areaCompensation = compensateArea ? 1f / effectiveSubsampleFactor : 1f;
            var radiusCompensationFloat = Mathf.Clamp(Mathf.Sqrt(areaCompensation), 0f, 255f);
            var radiusCompensationByte = (byte)Mathf.RoundToInt(radiusCompensationFloat);

            Debug.Log("Vertex count reduced by: " + ((1f - effectiveSubsampleFactor) * 100f).ToString("0.00") + "%");


            int k = 0;
            for (int i = 0; i < originalCount; i++)
            {
                if (!invalids[i])
                {
                    newVertices[k] = originalVertices[i];
                    newColors32[k] = new Color32(originalColors32[i].r, originalColors32[i].g, originalColors32[i].b, radiusCompensationByte);
                    k++;
                }
            }

            return MeshFromArrays(newVertices, newColors32, outputName, originalMesh.bounds);
        }

        private static void MarkInvalidVerticesSpatial(Vector3[] vertices, bool[] invalids, float squareDistanceLimit, Range range = null)
        {
            int lowerBoundInclusive = range == null ? 0 : range.Lower;
            int upperBoundExclusive = range == null ? vertices.Length : range.Upper + 1;

            for (int i = lowerBoundInclusive; i < upperBoundExclusive; i++)
            {
                if (invalids[i] == true) continue; // we have already stripped this vertex

                for (int j = lowerBoundInclusive; j < upperBoundExclusive; j++)
                {
                    if (i == j) continue; // this is the same vertex, don't compare
                    if (invalids[j] == true) continue; // we have already stripped this vertex. don't compare

                    var sqrDistance = Vector3.SqrMagnitude(vertices[i] - vertices[j]);
                    if (sqrDistance < squareDistanceLimit) invalids[j] = true; // this vertex is too close, strip it
                }
            }
        }

        private static Mesh MeshFromArrays(Vector3[] vertices, Color32[] colors32, string name, Bounds bounds)
        {
            if (vertices.Length != colors32.Length) throw new System.Exception("Vertex and color array lengths do not match");

            Mesh mesh = new();
            mesh.indexFormat = vertices.Length > 65536 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors32);
            int[] indices = new int[vertices.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetIndices(indices, MeshTopology.Points, 0);
            mesh.bounds = bounds;
            mesh.name = name;

            return mesh;
        }

        private static int SumBools(bool[] bools)
        {
            int sum = 0;
            foreach (bool b in bools)
            {
                if (b) sum++;
            }
            return sum;
        }

        // https://stackoverflow.com/questions/1841246/c-sharp-splitting-an-array
        public static IEnumerable<IEnumerable<T>> Split<T>(this T[] array, int size)
        {
            for (var i = 0; i < (float)array.Length / size; i++)
            {
                yield return array.Skip(i * size).Take(size);
            }
        }
    }
}