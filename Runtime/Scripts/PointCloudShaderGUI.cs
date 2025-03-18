using UnityEngine;
using UnityEditor;

namespace StoryLabResearch.PointCloud
{
    public class PointCloudShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            // Find properties
            MaterialProperty _PointSize = FindProperty("_PointSize", properties);
            MaterialProperty _ColorMode = FindProperty("_ColorMode", properties);
            MaterialProperty _Color = FindProperty("_Color", properties);
            MaterialProperty _ColorBlend = FindProperty("_ColorBlend", properties);
            MaterialProperty _VRScale = FindProperty("_VRScale", properties, false);
            MaterialProperty _DistanceFade = FindProperty("_DistanceFade", properties, false);
            MaterialProperty _DebugCrossfade = FindProperty("_Debug", properties, false);

            // Create foldout sections
            bool baseSettingsFoldout = EditorGUILayout.Foldout(
                SessionState.GetBool("PointCloudShaderGUI_BaseSettings", true), 
                "Base Settings", 
                true
            );
            SessionState.SetBool("PointCloudShaderGUI_BaseSettings", baseSettingsFoldout);

            if (baseSettingsFoldout)
            {
                EditorGUI.indentLevel++;
                
                // Display base properties
                materialEditor.ShaderProperty(_PointSize, _PointSize.displayName);
                
                materialEditor.ShaderProperty(_ColorMode, _ColorMode.displayName);

                if (_ColorMode.floatValue > 0) // Solid color mode
                {
                    materialEditor.ShaderProperty(_Color, _Color.displayName);

                    if (_ColorMode.floatValue > 1) // Blend mode
                    {
                        materialEditor.ShaderProperty(_ColorBlend, _ColorBlend.displayName);
                    }
                }
                
                EditorGUI.indentLevel--;
            }

            // VR Optimization Settings
            bool vrSettingsFoldout = EditorGUILayout.Foldout(
                SessionState.GetBool("PointCloudShaderGUI_VRSettings", true), 
                "VR Optimization Settings", 
                true
            );
            SessionState.SetBool("PointCloudShaderGUI_VRSettings", vrSettingsFoldout);

            if (vrSettingsFoldout)
            {
                EditorGUI.indentLevel++;
                
                // Display VR-specific properties if they exist
                if (_VRScale != null)
                {
                    materialEditor.ShaderProperty(_VRScale, "VR Point Size Scale");
                    
                    EditorGUILayout.HelpBox(
                        "Adjust this value to control point size in VR. Lower values improve performance but reduce visual quality.", 
                        MessageType.Info
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "This material doesn't support VR optimization. Consider using the 'StoryLab PointCloud/URP_Mobile' shader for VR projects.",
                        MessageType.Warning
                    );
                }
                
                if (_DistanceFade != null)
                {
                    materialEditor.ShaderProperty(_DistanceFade, "Distance Fade Factor");
                    
                    EditorGUILayout.HelpBox(
                        "Higher values make distant points smaller, improving performance. Lower values maintain consistent sizes.",
                        MessageType.Info
                    );
                }
                
                EditorGUI.indentLevel--;
            }

            // Debug Settings
            bool debugSettingsFoldout = EditorGUILayout.Foldout(
                SessionState.GetBool("PointCloudShaderGUI_DebugSettings", false), 
                "Debug Settings", 
                true
            );
            SessionState.SetBool("PointCloudShaderGUI_DebugSettings", debugSettingsFoldout);

            if (debugSettingsFoldout)
            {
                EditorGUI.indentLevel++;
                
                if (_DebugCrossfade != null)
                {
                    materialEditor.ShaderProperty(_DebugCrossfade, "Debug LOD Crossfade");
                }
                
                EditorGUI.indentLevel--;
            }
            
            // GPU info and recommendations section
            EditorGUILayout.Space();
            bool performanceInfoFoldout = EditorGUILayout.Foldout(
                SessionState.GetBool("PointCloudShaderGUI_PerformanceInfo", false),
                "Performance Information",
                true
            );
            SessionState.SetBool("PointCloudShaderGUI_PerformanceInfo", performanceInfoFoldout);
            
            if (performanceInfoFoldout)
            {
                EditorGUILayout.HelpBox(
                    "Unity 6 Point Cloud Renderer Optimizations:\n\n" +
                    "• For Meta Quest or mobile VR, use 'URP_Mobile' shader\n" +
                    "• For PC VR, use the standard 'URP' shader\n" +
                    "• Add 'VRPointCloudOptimizer' component for dynamic performance tuning\n" +
                    "• Create smaller chunks for better culling (2-5 units recommended for VR)\n" +
                    "• Use LODs with CrossFade enabled for smoother transitions", 
                    MessageType.Info
                );
            }
        }
    }
}
