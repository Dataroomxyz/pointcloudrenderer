EditorGUILayout.Space();
                
                // LOD Section
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("LOD Settings", EditorStyles.boldLabel);
                
                EditorGUILayout.PropertyField(_generateLODs, _generateLODsLabel);
                if (_generateLODs.boolValue)
                {
                    EditorGUILayout.PropertyField(_crossFadeLODs, _crossFadeLODsLabel);
                    EditorGUILayout.PropertyField(_fastLODGeneration, _fastLODGenerationLabel);
                    EditorGUILayout.PropertyField(_lodDescriptions);
                    
                    if (_optimizeForVR.boolValue)
                    {
                        EditorGUILayout.HelpBox(
                            "LOD settings will be automatically adjusted based on VR optimization level. " +
                            "These base settings will still be used but with VR-specific modifications.",
                            MessageType.Info);
                    }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
                
                // Material Section
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Material Settings", EditorStyles.boldLabel);

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
                
                if (_optimizeForVR.boolValue && _materialMode.intValue != (int)PlyImporter.EMaterialMode.Custom)
                {
                    EditorGUILayout.HelpBox("When VR optimization is enabled, a mobile-optimized shader will be used automatically.", MessageType.Info);
                }
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            
            // Debug Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Debug Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_debugRangeColors);
            if(_debugRangeColors.boolValue) _debugRange.intValue = Mathf.Max(0, EditorGUILayout.IntField(_debugRangeLabel, _debugRange.intValue));
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
        
        private string GetVROptimizationLevelDescription(int level)
        {
            switch(level)
            {
                case 0: // None
                    return "No optimization: Use the standard rendering approach with no VR-specific optimizations.";
                    
                case 1: // Low
                    return "Low optimization: Minor adjustments for VR. Good for high-end VR devices like PC VR or Quest Pro.";
                    
                case 2: // Medium
                    return "Medium optimization: Balanced performance and visual quality. Recommended for Quest 3.";
                    
                case 3: // High
                    return "High optimization: Prioritize performance over visual quality. Good for complex scenes or Quest 2.";
                    
                case 4: // Extreme
                    return "Extreme optimization: Maximum performance at the cost of visual quality. For very complex scenes or older devices.";
                    
                default:
                    return "Unknown optimization level";
            }
        }
    }
}
