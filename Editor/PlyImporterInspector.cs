using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.AssetImporters;
using UnityEditor;

namespace StoryLabResearch.PointCloud
{
    [CustomEditor(typeof(PlyImporter))]
    public class PlyImporterInspector : ScriptedImporterEditor
    {
        SerializedProperty _rescale;
        SerializedProperty _applySRGBCorrection;

        SerializedProperty _containerType;
        string[] _containerTypeNames;

        SerializedProperty _initialSubsampleMode;
        SerializedProperty _initialSubsampleValue;

        SerializedProperty _materialMode;
        SerializedProperty _customMaterialOverride;
        SerializedProperty _extractUniqueMaterial;

        SerializedProperty _generateChunks;
        SerializedProperty _chunkSize;

        SerializedProperty _generateLODs;
        GUIContent _generateLODsLabel = new("Generate LODs");
        SerializedProperty _crossFadeLODs;
        GUIContent _crossFadeLODsLabel = new("CrossFade LODs");
        SerializedProperty _fastLODGeneration;
        GUIContent _fastLODGenerationLabel = new("Fast LOD Generation");
        SerializedProperty _lodDescriptions;

        SerializedProperty _debugRangeColors;
        GUIContent _debugRangeLabel = new("Debug Range");
        SerializedProperty _debugRange;

        public override void OnEnable()
        {
            base.OnEnable();

            _rescale = serializedObject.FindProperty(nameof(PlyImporter.Rescale));
            _applySRGBCorrection = serializedObject.FindProperty(nameof(PlyImporter.ApplySRGBCorrection));

            _containerType = serializedObject.FindProperty(nameof(PlyImporter.ContainerType));
            _containerTypeNames = System.Enum.GetNames(typeof(PlyImporter.EAssetContainerType));

            _initialSubsampleMode = serializedObject.FindProperty(nameof(PlyImporter.InitialSubsampleMode));
            _initialSubsampleValue = serializedObject.FindProperty(nameof(PlyImporter.SubsampleValue));

            _materialMode = serializedObject.FindProperty(nameof(PlyImporter.MaterialMode));
            _customMaterialOverride = serializedObject.FindProperty(nameof(PlyImporter.CustomMaterialOverride));
            _extractUniqueMaterial = serializedObject.FindProperty(nameof(PlyImporter.ExtractUniqueMaterial));

            _generateChunks = serializedObject.FindProperty(nameof(PlyImporter.GenerateChunks));
            _chunkSize = serializedObject.FindProperty(nameof(PlyImporter.ChunkSize));

            _generateLODs = serializedObject.FindProperty(nameof(PlyImporter.GenerateLODs));
            _crossFadeLODs = serializedObject.FindProperty(nameof(PlyImporter.CrossFadeLODs));
            _fastLODGeneration = serializedObject.FindProperty(nameof(PlyImporter.FastLODGeneration));
            _lodDescriptions = serializedObject.FindProperty(nameof(PlyImporter.LODDescriptions));

            _debugRangeColors = serializedObject.FindProperty(nameof(PlyImporter.DebugRangeColors));
            _debugRange = serializedObject.FindProperty(nameof(PlyImporter.DebugRange));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(_rescale);

            EditorGUILayout.PropertyField(_applySRGBCorrection);
            _containerType.intValue = EditorGUILayout.Popup(
                "Container Type", _containerType.intValue, _containerTypeNames);

            EditorGUILayout.Space();

            if (_containerType.intValue == (int)PlyImporter.EAssetContainerType.PointMesh)
            {
                EditorGUILayout.PropertyField(_initialSubsampleMode);
                if (_initialSubsampleMode.intValue == (int)PointMeshSubsampler.ESubsampleMode.Random) _initialSubsampleValue.floatValue = EditorGUILayout.Slider("Subsample Factor", _initialSubsampleValue.floatValue, 0f, 1f);
                else if (_initialSubsampleMode.intValue == (int)PointMeshSubsampler.ESubsampleMode.SpatialFast 
                      || _initialSubsampleMode.intValue == (int)PointMeshSubsampler.ESubsampleMode.SpatialThreePass
                      || _initialSubsampleMode.intValue == (int)PointMeshSubsampler.ESubsampleMode.SpatialExact)
                    _initialSubsampleValue.floatValue = EditorGUILayout.FloatField("Minimum Distance", Mathf.Max(_initialSubsampleValue.floatValue, 0f));

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(_generateChunks);
                if (_generateChunks.boolValue) EditorGUILayout.PropertyField(_chunkSize);

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(_generateLODs, _generateLODsLabel);
                if (_generateLODs.boolValue)
                {
                    EditorGUILayout.PropertyField(_crossFadeLODs, _crossFadeLODsLabel);
                    EditorGUILayout.PropertyField(_fastLODGeneration, _fastLODGenerationLabel);
                    EditorGUILayout.PropertyField(_lodDescriptions);
                }

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(_materialMode);
                switch(_materialMode.intValue)
                {
                    case (int)PlyImporter.EMaterialMode.Unique:
                        EditorGUILayout.HelpBox("Create a unique material for this asset.\r\n\r\nBy default the material will be extracted to allow you to edit its properties. The extracted material will not be destroyed if you choose later not to extract it.", MessageType.Info);
                        EditorGUILayout.PropertyField(_extractUniqueMaterial);
                        break;
                    case (int)PlyImporter.EMaterialMode.Custom:
                        EditorGUILayout.HelpBox("Apply a custom user-selected material to this asset.\r\n\r\nThis can allow you to share a material with custom properties across many point cloud assets for better rendering efficiency.", MessageType.Info);
                        EditorGUILayout.PropertyField(_customMaterialOverride);
                        break;
                    default:
                        EditorGUILayout.HelpBox("Use the default shared point cloud rendering material, for more efficient rendering if you do not need to edit material properties.\r\n\r\nEditing the default material will affect all current and future point cloud assets set to this mode, and should be avoided - if you need custom properties, create a custom material instead.", MessageType.Warning);
                        break;
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_debugRangeColors);
            if(_debugRangeColors.boolValue) _debugRange.intValue = Mathf.Max(0, EditorGUILayout.IntField(_debugRangeLabel, _debugRange.intValue));

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
    }
}