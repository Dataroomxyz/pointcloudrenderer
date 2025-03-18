return Mathf.Min(ChunkSize, 2f);
                default:
                    return ChunkSize;
            }
        }

        private static void CreateAndAddSingleMeshObject(AssetImportContext context, Material material, Mesh mesh, GameObject gameObject)
        {
            var meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            // Add point topology converter component to ensure proper rendering
            gameObject.AddComponent<MeshPointTopologyConverter>();

            context.AddObjectToAsset(mesh.name, mesh);
        }

        private void CreateAndAddLODGroupObject(AssetImportContext context, Mesh mesh, Material material, GameObject gameObject, LODDescription[] lodDescriptions, bool crossFadeLODs, bool fastLODGeneration)
        {
            var lodGroup = gameObject.AddComponent<LODGroup>();

            // Customize LOD transitions for VR if optimizing for VR
            if (OptimizeForVR)
            {
                lodDescriptions = GetVROptimizedLODDescriptions(lodDescriptions);
            }

            var LODs = CreateAndAddLODRenderers(context, mesh, material, gameObject, lodDescriptions, fastLODGeneration);

            lodGroup.SetLODs(LODs);
            lodGroup.fadeMode = crossFadeLODs ? LODFadeMode.CrossFade : LODFadeMode.None;
            
            // Set animation time to a faster value for VR
            if (OptimizeForVR && crossFadeLODs)
            {
                // Use reflection to set the crossfade animation time (normally 0.5s)
                // NOTE: This is a private field, so may need to be updated for Unity 6
                var animTimeField = lodGroup.GetType().GetField("m_AnimateCrossFading", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (animTimeField != null)
                {
                    animTimeField.SetValue(lodGroup, true);
                }
                
                var fadeTimeField = lodGroup.GetType().GetField("m_CrossFadeAnimationDuration", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fadeTimeField != null)
                {
                    // Use a shorter crossfade time for VR to reduce performance impact
                    fadeTimeField.SetValue(lodGroup, 0.25f);
                }
            }
        }
        
        private LODDescription[] GetVROptimizedLODDescriptions(LODDescription[] original)
        {
            // Create a copy to avoid modifying the original
            var optimized = new LODDescription[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                optimized[i] = new LODDescription
                {
                    SubsampleMode = original[i].SubsampleMode,
                    SubsampleValue = original[i].SubsampleValue,
                    ScreenRelativeTransitionHeight = original[i].ScreenRelativeTransitionHeight,
                    FadeTransitionWidth = original[i].FadeTransitionWidth,
                    CompensateArea = original[i].CompensateArea
                };
            }
            
            // Adjust settings based on VR optimization level
            switch (VROptimizationLevel)
            {
                case EVROptimizationLevel.Low:
                    // Small adjustments to transition heights
                    for (int i = 0; i < optimized.Length; i++)
                    {
                        optimized[i].ScreenRelativeTransitionHeight *= 0.9f;
                        optimized[i].FadeTransitionWidth = Mathf.Min(optimized[i].FadeTransitionWidth, 0.08f);
                    }
                    break;
                    
                case EVROptimizationLevel.Medium:
                    // More aggressive LOD transitions
                    for (int i = 0; i < optimized.Length; i++)
                    {
                        optimized[i].ScreenRelativeTransitionHeight *= 0.8f;
                        optimized[i].FadeTransitionWidth = Mathf.Min(optimized[i].FadeTransitionWidth, 0.06f);
                        
                        // Increase subsample values slightly for more aggressive culling
                        if (optimized[i].SubsampleMode == PointMeshSubsampler.ESubsampleMode.SpatialFast)
                        {
                            optimized[i].SubsampleValue *= 1.2f;
                        }
                    }
                    break;
                    
                case EVROptimizationLevel.High:
                    // Very aggressive LOD transitions
                    for (int i = 0; i < optimized.Length; i++)
                    {
                        optimized[i].ScreenRelativeTransitionHeight *= 0.65f;
                        optimized[i].FadeTransitionWidth = Mathf.Min(optimized[i].FadeTransitionWidth, 0.04f);
                        
                        // Increase subsample values for more aggressive culling
                        if (optimized[i].SubsampleMode == PointMeshSubsampler.ESubsampleMode.SpatialFast)
                        {
                            optimized[i].SubsampleValue *= 1.5f;
                        }
                    }
                    break;
                    
                case EVROptimizationLevel.Extreme:
                    // Maximum optimization for low-end devices
                    for (int i = 0; i < optimized.Length; i++)
                    {
                        optimized[i].ScreenRelativeTransitionHeight *= 0.5f;
                        optimized[i].FadeTransitionWidth = Mathf.Min(optimized[i].FadeTransitionWidth, 0.02f);
                        
                        // Increase subsample values for maximum culling
                        if (optimized[i].SubsampleMode == PointMeshSubsampler.ESubsampleMode.SpatialFast)
                        {
                            optimized[i].SubsampleValue *= 2.0f;
                        }
                    }
                    break;
            }
            
            return optimized;
        }

        private LOD[] CreateAndAddLODRenderers(AssetImportContext context, Mesh mesh, Material material, GameObject gameObject, LODDescription[] lodDescriptions, bool fastLODGeneration)
        {
            LODDescription[] OrderedLODDescriptions = OrderLODDescriptionsIfNecessary(lodDescriptions);
            var LODs = new LOD[OrderedLODDescriptions.Length];
            var workingMesh = mesh;
            var referencePointCount = mesh.vertexCount;

            for (int i = 0; i < lodDescriptions.Length; i++)
            {
                Mesh subsampledMesh = PointMeshSubsampler.SubsampleMesh(OrderedLODDescriptions[i].SubsampleMode, workingMesh, mesh.name + "_LOD" + i, OrderedLODDescriptions[i].SubsampleValue, referencePointCount, true);
                context.AddObjectToAsset(subsampledMesh.name, subsampledMesh);
                LODs[i] = CreateAddAndReturnLODRendererObject(context, gameObject, subsampledMesh, OrderedLODDescriptions[i], material);

                if (fastLODGeneration) workingMesh = subsampledMesh; // use the already subsampled output mesh as the start point for the next LOD level
            }

            return LODs;
        }

        private LODDescription[] OrderLODDescriptionsIfNecessary(LODDescription[] lodDescriptions)
        {
            LODDescription[] orderedLODDescriptions = lodDescriptions;
            for (int i = 1; i < lodDescriptions.Length; i++)
            {
                if (lodDescriptions[i].ScreenRelativeTransitionHeight > lodDescriptions[i - 1].ScreenRelativeTransitionHeight)
                {
                    Debug.LogWarning("LOD Descriptions are not in transition size order, reordering...");
                    orderedLODDescriptions = lodDescriptions.OrderByDescending(o => o.ScreenRelativeTransitionHeight).ToArray();
                    break;
                }
            }
            return orderedLODDescriptions;
        }

        private LOD CreateAddAndReturnLODRendererObject(AssetImportContext context, GameObject gameObject, Mesh subsampledMesh, LODDescription description, Material material)
        {
            var subGameObject = new GameObject();
            subGameObject.transform.parent = gameObject.transform;
            subGameObject.name = subsampledMesh.name;

            var meshFilter = subGameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = subsampledMesh;

            var meshRenderer = subGameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            
            // Add point topology converter component to ensure proper rendering
            subGameObject.AddComponent<MeshPointTopologyConverter>();

            var LOD = new LOD
            {
                screenRelativeTransitionHeight = description.ScreenRelativeTransitionHeight,
                fadeTransitionWidth = description.FadeTransitionWidth,
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
                if (USE_ALPHA_FOR_POINT_SIZE_MULTIPLER) a = 1;
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

            if (DebugRangeColors)
            {
                int colorChangeTracker = 0;
                int colorChangeInterval = DebugRange;
                for (int i = 0; i < header.vertexCount; i++)
                {
                    if (colorChangeTracker >= colorChangeInterval)
                    {
                        colorChangeTracker = 0;
                        r = (byte)UnityEngine.Random.Range(0, 256);
                        g = (byte)UnityEngine.Random.Range(0, 256);
                        b = (byte)UnityEngine.Random.Range(0, 256);
                    }

                    data.colors[i] = new Color32(r, g, b, data.colors[i].a);
                    colorChangeTracker++;
                }
            }

            return data;
        }
        #endregion
    }
}
