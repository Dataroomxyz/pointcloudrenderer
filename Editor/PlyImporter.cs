using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StoryLabResearch.PointCloud
{
    [System.Serializable]
    public class LODDescription
    {
        [Range(0f, 1f)] public float subsampleFactor = 1;
        [Min(0f)] public float screenRelativeTransitionHeightPerUnit = .2f;
        [Range(0f, 1f)] public float fadeTransitionWidth = .1f;
        public bool compensateArea = true;
    }

    [ScriptedImporter(1, "ply")]
    class PlyImporter : ScriptedImporter
    {
        public PlyImporter()
        {
            LODDescriptions = new LODDescription[]
            {
            new LODDescription { subsampleFactor = 1.0f, screenRelativeTransitionHeightPerUnit = 2.5f, fadeTransitionWidth = 0.1f },
            new LODDescription { subsampleFactor = 0.5f, screenRelativeTransitionHeightPerUnit = 1.0f, fadeTransitionWidth = 0.1f },
            new LODDescription { subsampleFactor = 0.1f, screenRelativeTransitionHeightPerUnit = 0.25f, fadeTransitionWidth = 0.1f },
            new LODDescription { subsampleFactor = 0.01f, screenRelativeTransitionHeightPerUnit = 0.025f, fadeTransitionWidth = 0.1f }
            };
            GenerateLODs = true;
        }
        public enum EAssetContainerType { PointMesh, BakedTexture }
        public enum EMaterialMode { Unique, Custom, ShareDefault }

        #region ScriptedImporter implementation

        public float Rescale = 1.0f;
        public bool ApplySRGBCorrection;
        public EAssetContainerType ContainerType = EAssetContainerType.PointMesh;

        public bool Subsample = false;
        [Range(0f, 1f)] public float SubsampleFactor = 1f;

        public EMaterialMode MaterialMode = EMaterialMode.Unique;
        public Material CustomMaterialOverride;
        public bool ExtractUniqueMaterial = true;

        public bool GenerateLODs = true;
        public bool CrossFadeLODs = true;
        public LODDescription[] LODDescriptions;

        public bool GenerateChunks = true;
        public float ChunkSize = 5f;

        public static readonly string SHADER_PATH = "Packages/com.storylabresearch.pointcloudrenderer/runtime/shaders/";
        private const bool USE_ALPHA_FOR_POINT_SIZE_MULTIPLER = true;
        // shader uses alpha * 0.1 as a point size multiplier so LODs don't need unique shaders
        // this means we can multiply the size of the point in a range from 0.0 - 25.5
        // this is sufficient to cover the expected decimations of 10x and 100x
        // (requiring size multipliers of ~3.2 and 10 respectively to compensate the area loss)
        // The only reason this is a const is to make it really obvious what I'm doing here

        public override void OnImportAsset(AssetImportContext context)
        {
            switch (ContainerType)
            {
                case EAssetContainerType.PointMesh:
                    ImportAsPointMesh(context);
                    break;
                case EAssetContainerType.BakedTexture:
                    ImportAsBakedTexture(context);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void ImportAsBakedTexture(AssetImportContext context)
        {
            var data = ReadDataAsBaked(context.assetPath);
            if (data != null)
            {
                context.AddObjectToAsset("container", data);
                context.AddObjectToAsset("position", data.positionMap);
                context.AddObjectToAsset("color", data.colorMap);
                context.SetMainObject(data);
            }
        }

        private void ImportAsPointMesh(AssetImportContext context)
        {
            var rootGameObject = new GameObject();

            Mesh plyMesh = GetPlyMesh(context);
            Material material = GetAndAddMaterial(context, plyMesh.name);
            CreateAndAddMeshObjects(context, rootGameObject, material, plyMesh);

            context.AddObjectToAsset("prefab", rootGameObject);
            context.SetMainObject(rootGameObject);
        }

        #endregion

        #region Internal utilities

        private Mesh GetPlyMesh(AssetImportContext context)
        {
            var plyMesh = ReadDataAsMesh(context.assetPath, false);
            if (Subsample && SubsampleFactor < 1f) plyMesh = PointMeshSubsampler.SubsampleMesh(plyMesh, SubsampleFactor);
            return plyMesh;
        }

        private Material GetAndAddMaterial(AssetImportContext context, string name)
        {
            Material material;
            switch (MaterialMode)
            {
                case EMaterialMode.Unique:
                    material = new(GetPipelineSpecificDefaultMaterial().shader);
                    material.name = name;
                    if (ExtractUniqueMaterial) AssetDatabase.CreateAsset(material, Path.Combine(Path.GetDirectoryName(context.assetPath), Path.GetFileName(context.assetPath) + ".mat"));
                    else context.AddObjectToAsset("Unique Material", material);
                    break;

                case EMaterialMode.Custom:
                    material = CustomMaterialOverride;
                    break;

                default:
                    material = GetPipelineSpecificDefaultMaterial();
                    break;
            }
            return material;
        }

        static Material GetPipelineSpecificDefaultMaterial()
        {
            var path = SHADER_PATH;

            if (GraphicsSettings.currentRenderPipeline != null)
            {
                if (GraphicsSettings.currentRenderPipeline.name.Contains("URP"))
                {
                    path += "URP";
                }
                else if (GraphicsSettings.currentRenderPipeline.name.Contains("HDRP"))
                {
                    throw new Exception("Current Scriptable Render Pipeline is HDRP, which is not currently supported!");
                    //path += "HDRP";
                }
                else
                {
                    throw new Exception("Graphics settings contains a Scriptable Render Pipeline, but it is not URP or HDRP! Aborting");
                }
            }
            else
            {
                throw new Exception("BRP shaders have not yet been brought up to parity with URP ones - sorry!");
                //path += "BRP";
            }

            path += "/DefaultPointCloud.mat";

            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private void CreateAndAddMeshObjects(AssetImportContext context, GameObject rootGameObject, Material material, Mesh plyMesh)
        {
            Mesh[] meshes;
            if (GenerateChunks) meshes = PointMeshChunker.ChunkPointMesh(plyMesh, Vector3.zero, ChunkSize);
            else meshes = new Mesh[] { plyMesh };

            foreach (var mesh in meshes)
            {
                var gameObject = new GameObject();
                gameObject.transform.parent = rootGameObject.transform;
                gameObject.name = mesh.name;

                if (GenerateLODs) CreateAndAddLODGroupObject(context, mesh, material, gameObject);
                else CreateAndAddSingleMeshObject(context, material, mesh, gameObject);

                context.AddObjectToAsset(mesh.name + " object", gameObject);
            }

            Debug.Log("Mesh count: " + meshes.Length * (GenerateLODs ? LODDescriptions.Length : 1));
        }

        private static void CreateAndAddSingleMeshObject(AssetImportContext context, Material material, Mesh mesh, GameObject gameObject)
        {
            var meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            context.AddObjectToAsset(mesh.name, mesh);
        }

        private void CreateAndAddLODGroupObject(AssetImportContext context, Mesh mesh, Material material, GameObject gameObject)
        {
            var lodGroup = gameObject.AddComponent<LODGroup>();
            var LODs = new LOD[LODDescriptions.Length];

            LODDescription[] OrderedLODDescriptions = OrderLODDescriptionsIfNecessary();

            for (int i = 0; i < LODDescriptions.Length; i++)
            {
                LODs[i] = CreateAddAndReturnLODMeshObject(context, gameObject, mesh, OrderedLODDescriptions[i], material, "_LOD" + i);
            }

            lodGroup.SetLODs(LODs);
            lodGroup.fadeMode = CrossFadeLODs ? LODFadeMode.CrossFade : LODFadeMode.None;
        }

        private LODDescription[] OrderLODDescriptionsIfNecessary()
        {
            LODDescription[] OrderedLODDescriptions = LODDescriptions;
            for (int i = 1; i < LODDescriptions.Length; i++)
            {
                if (LODDescriptions[i].screenRelativeTransitionHeightPerUnit > LODDescriptions[i - 1].screenRelativeTransitionHeightPerUnit)
                {
                    Debug.LogWarning("LOD Descriptions are not in transition size order, reordering...");
                    OrderedLODDescriptions = LODDescriptions.OrderByDescending(o => o.screenRelativeTransitionHeightPerUnit).ToArray();
                    break;
                }
            }
            return OrderedLODDescriptions;
        }

        private LOD CreateAddAndReturnLODMeshObject(AssetImportContext context, GameObject gameObject, Mesh originalMesh, LODDescription description, Material material, string suffix = "")
        {
            Mesh subsampledMesh = PointMeshSubsampler.SubsampleMesh(originalMesh, description.subsampleFactor, suffix);
            context.AddObjectToAsset(subsampledMesh.name, subsampledMesh);

            var subGameObject = new GameObject();
            subGameObject.transform.parent = gameObject.transform;
            subGameObject.name = subsampledMesh.name;

            var meshFilter = subGameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = subsampledMesh;

            var meshRenderer = subGameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            var LOD = new LOD
            {
                screenRelativeTransitionHeight = description.screenRelativeTransitionHeightPerUnit / ChunkSize,
                fadeTransitionWidth = description.fadeTransitionWidth,
                renderers = new Renderer[] { meshRenderer }
            };

            context.AddObjectToAsset(subsampledMesh.name + " object", subGameObject);

            return LOD;
        }

        #endregion

        #region Internal data structure
        // https://github.com/keijiro/Pcx
        // Code used under the UnLicense

        enum DataProperty
        {
            Invalid,
            R8, G8, B8, A8,
            R16, G16, B16, A16,
            SingleX, SingleY, SingleZ,
            DoubleX, DoubleY, DoubleZ,
            Data8, Data16, Data32, Data64
        }

        static int GetPropertySize(DataProperty p)
        {
            switch (p)
            {
                case DataProperty.R8: return 1;
                case DataProperty.G8: return 1;
                case DataProperty.B8: return 1;
                case DataProperty.A8: return 1;
                case DataProperty.R16: return 2;
                case DataProperty.G16: return 2;
                case DataProperty.B16: return 2;
                case DataProperty.A16: return 2;
                case DataProperty.SingleX: return 4;
                case DataProperty.SingleY: return 4;
                case DataProperty.SingleZ: return 4;
                case DataProperty.DoubleX: return 8;
                case DataProperty.DoubleY: return 8;
                case DataProperty.DoubleZ: return 8;
                case DataProperty.Data8: return 1;
                case DataProperty.Data16: return 2;
                case DataProperty.Data32: return 4;
                case DataProperty.Data64: return 8;
                case DataProperty.Invalid: break;
                default: break;
            }
            return 0;
        }

        class DataHeader
        {
            public List<DataProperty> properties = new();
            public int vertexCount = -1;
        }

        class DataBody
        {
            public List<Vector3> vertices;
            public List<Color32> colors;

            public DataBody(int vertexCount)
            {
                vertices = new List<Vector3>(vertexCount);
                colors = new List<Color32>(vertexCount);
            }

            public void AddPoint(
                float x, float y, float z,
                byte r, byte g, byte b, byte a,
                float rescale = 1.0f,
                bool applySRGBCorrection = false
            )
            {
                vertices.Add(new Vector3(x, y, z) * rescale);
                if (USE_ALPHA_FOR_POINT_SIZE_MULTIPLER) a = 10;
                if (applySRGBCorrection) colors.Add(new Color32(CorrectSRGB(r), CorrectSRGB(g), CorrectSRGB(b), a));
                else colors.Add(new Color32(r, g, b, a));
            }

            private byte CorrectSRGB(byte val, bool reverse = false)
            {
                var floatVal = (float)val / 255;
                var correctedFloatVal = Mathf.Pow(floatVal, reverse ? 2.2f : 1.0f / 2.2f);
                return (byte)Mathf.RoundToInt(correctedFloatVal * 255);
            }
        }

        #endregion

        #region Reader implementation

        Mesh ReadDataAsMesh(string path, bool makeReadable)
        {
            try
            {
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = ReadDataHeader(new StreamReader(stream));
                var body = ReadDataBody(header, new BinaryReader(stream));

                Mesh mesh = new();
                mesh.name = Path.GetFileNameWithoutExtension(path);

                mesh.indexFormat = header.vertexCount > 65535 ?
                    IndexFormat.UInt32 : IndexFormat.UInt16;

                mesh.SetVertices(body.vertices);
                mesh.SetColors(body.colors);

                mesh.SetIndices(
                    Enumerable.Range(0, header.vertexCount).ToArray(),
                    MeshTopology.Points, 0
                );

                stream.Close();

                mesh.UploadMeshData(!makeReadable);
                return mesh;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }

        BakedPointCloud ReadDataAsBaked(string path, string prefix = "")
        {
            try
            {
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = ReadDataHeader(new StreamReader(stream));
                var body = ReadDataBody(header, new BinaryReader(stream));
                var data = ScriptableObject.CreateInstance<BakedPointCloud>();
                data.Initialize(prefix, body.vertices, body.colors);
                data.name = prefix + "Data";

                stream.Close();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }

        DataHeader ReadDataHeader(StreamReader reader)
        {
            var data = new DataHeader();
            var readCount = 0;

            // Magic number line ("ply")
            var line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "ply")
                throw new ArgumentException("Magic number ('ply') mismatch.");

            // Data format: check if it's binary/little endian.
            line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "format binary_little_endian 1.0")
                throw new ArgumentException(
                    "Invalid data format ('" + line + "'). " +
                    "Should be binary/little endian.");

            // Read header contents.
            for (var skip = false; ;)
            {
                // Read a line and split it with white space.
                line = reader.ReadLine();
                readCount += line.Length + 1;
                if (line == "end_header") break;
                var col = line.Split();

                // Element declaration (unskippable)
                if (col[0] == "element")
                {
                    if (col[1] == "vertex")
                    {
                        data.vertexCount = Convert.ToInt32(col[2]);
                        skip = false;
                    }
                    else
                    {
                        // Don't read elements other than vertices.
                        skip = true;
                    }
                }

                if (skip) continue;

                // Property declaration line
                if (col[0] == "property")
                {
                    var prop = DataProperty.Invalid;

                    // Parse the property name entry.
                    switch (col[2])
                    {
                        case "red": prop = DataProperty.R8; break;
                        case "green": prop = DataProperty.G8; break;
                        case "blue": prop = DataProperty.B8; break;
                        case "alpha": prop = DataProperty.A8; break;
                        case "x": prop = DataProperty.SingleX; break;
                        case "y": prop = DataProperty.SingleY; break;
                        case "z": prop = DataProperty.SingleZ; break;
                    }

                    // Check the property type.
                    if (col[1] == "char" || col[1] == "uchar" ||
                        col[1] == "int8" || col[1] == "uint8")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data8;
                        else if (GetPropertySize(prop) != 1)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "short" || col[1] == "ushort" ||
                                col[1] == "int16" || col[1] == "uint16")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data16; break;
                            case DataProperty.R8: prop = DataProperty.R16; break;
                            case DataProperty.G8: prop = DataProperty.G16; break;
                            case DataProperty.B8: prop = DataProperty.B16; break;
                            case DataProperty.A8: prop = DataProperty.A16; break;
                        }
                        if (GetPropertySize(prop) != 2)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int" || col[1] == "uint" || col[1] == "float" ||
                                col[1] == "int32" || col[1] == "uint32" || col[1] == "float32")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data32;
                        else if (GetPropertySize(prop) != 4)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int64" || col[1] == "uint64" ||
                                col[1] == "double" || col[1] == "float64")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data64; break;
                            case DataProperty.SingleX: prop = DataProperty.DoubleX; break;
                            case DataProperty.SingleY: prop = DataProperty.DoubleY; break;
                            case DataProperty.SingleZ: prop = DataProperty.DoubleZ; break;
                        }
                        if (GetPropertySize(prop) != 8)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else
                    {
                        throw new ArgumentException("Unsupported property type ('" + line + "').");
                    }

                    data.properties.Add(prop);
                }
            }

            // Rewind the stream back to the exact position of the reader.
            reader.BaseStream.Position = readCount;

            return data;
        }

        DataBody ReadDataBody(DataHeader header, BinaryReader reader)
        {
            var data = new DataBody(header.vertexCount);

            float x = 0, y = 0, z = 0;
            Byte r = 255, g = 255, b = 255, a = 255;

            for (var i = 0; i < header.vertexCount; i++)
            {
                foreach (var prop in header.properties)
                {
                    switch (prop)
                    {
                        case DataProperty.R8: r = reader.ReadByte(); break;
                        case DataProperty.G8: g = reader.ReadByte(); break;
                        case DataProperty.B8: b = reader.ReadByte(); break;
                        case DataProperty.A8: a = reader.ReadByte(); break;

                        case DataProperty.R16: r = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.G16: g = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.B16: b = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.A16: a = (byte)(reader.ReadUInt16() >> 8); break;

                        case DataProperty.SingleX: x = reader.ReadSingle(); break;
                        case DataProperty.SingleY: y = reader.ReadSingle(); break;
                        case DataProperty.SingleZ: z = reader.ReadSingle(); break;

                        case DataProperty.DoubleX: x = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleY: y = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleZ: z = (float)reader.ReadDouble(); break;

                        case DataProperty.Data8: reader.ReadByte(); break;
                        case DataProperty.Data16: reader.BaseStream.Position += 2; break;
                        case DataProperty.Data32: reader.BaseStream.Position += 4; break;
                        case DataProperty.Data64: reader.BaseStream.Position += 8; break;
                    }
                }

                data.AddPoint(x, y, z, r, g, b, a, Rescale, ApplySRGBCorrection);
            }

            return data;
        }
        #endregion
    }
}