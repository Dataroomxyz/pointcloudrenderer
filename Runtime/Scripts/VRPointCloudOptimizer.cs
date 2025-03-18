using UnityEngine;
using System.Collections;

namespace StoryLabResearch.PointCloud
{
    /// <summary>
    /// VR-specific optimizer for point cloud rendering on mobile VR devices such as Meta Quest 3
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("StoryLab PointCloud/VR Point Cloud Optimizer")]
    public class VRPointCloudOptimizer : MonoBehaviour
    {
        [Header("VR Performance Settings")]
        [Tooltip("Automatically adjust point cloud parameters for optimal VR performance")]
        public bool autoOptimize = true;
        
        [Tooltip("Target framerate to maintain (72/90/120 for Quest)")]
        public int targetFramerate = 72;
        
        [Range(0.1f, 3.0f)]
        [Tooltip("Scale factor for point size in VR (smaller values = better performance)")]
        public float vrPointSizeScale = 1.0f;
        
        [Range(0.0f, 10.0f)]
        [Tooltip("Distance fade factor (higher values fade points at shorter distances)")]
        public float distanceFade = 1.0f;
        
        [Header("Dynamic LOD Settings")]
        [Tooltip("Additional bias for LOD transitions in VR (negative values = higher quality)")]
        [Range(-1.0f, 2.0f)]
        public float vrLODBias = 0.0f;
        
        [Tooltip("Dynamically adjust LOD based on framerate")]
        public bool dynamicLOD = true;
        
        [Range(0.1f, 2.0f)]
        [Tooltip("Maximum dynamic LOD adjustment (higher = more aggressive)")]
        public float maxDynamicLODAdjustment = 0.5f;
        
        [Header("Preset Configurations")]
        [Tooltip("Apply predefined quality settings for different scenarios")]
        public VRQualityPreset qualityPreset = VRQualityPreset.Balanced;
        
        public enum VRQualityPreset
        {
            Minimum,      // Absolute minimum settings for low-end devices
            Performance,  // Prioritize framerate over visual quality
            Balanced,     // Balance between quality and performance
            Quality,      // Prioritize visual quality
            Maximum       // Maximum visual quality, may impact performance
        }
        
        [Header("Debug")]
        [Tooltip("Show performance statistics in editor")]
        public bool showStats = true;
        
        // Internal tracking
        private float _lastFrameTime;
        private float _averageFrameTime;
        private float _dynamicLODMultiplier = 1.0f;
        private const float _frameTimeSmoothing = 0.97f;
        private const float _dynamicLODSpeed = 0.1f;
        
        // Component references
        private MeshRenderer[] _renderers;
        private LODGroup _lodGroup;
        
        private void OnEnable()
        {
            // Find all renderers in children
            _renderers = GetComponentsInChildren<MeshRenderer>();
            _lodGroup = GetComponent<LODGroup>();
            
            // Initial setup
            ApplyQualityPreset();
            
            // Start the performance monitoring
            if (Application.isPlaying && autoOptimize)
            {
                StartCoroutine(MonitorPerformance());
            }
        }
        
        private void Update()
        {
            // Update VR-specific shader properties
            UpdateShaderProperties();
            
            // Update LOD settings if we have a LOD group
            if (_lodGroup != null)
            {
                UpdateLODSettings();
            }
        }
        
        private void UpdateShaderProperties()
        {
            if (_renderers == null || _renderers.Length == 0)
                return;
                
            foreach (var renderer in _renderers)
            {
                if (renderer == null || renderer.sharedMaterial == null)
                    continue;
                    
                // Update common material properties
                if (renderer.sharedMaterial.HasProperty("_VRScale"))
                    renderer.sharedMaterial.SetFloat("_VRScale", vrPointSizeScale * _dynamicLODMultiplier);
                    
                if (renderer.sharedMaterial.HasProperty("_DistanceFade"))
                    renderer.sharedMaterial.SetFloat("_DistanceFade", distanceFade);
            }
        }
        
        private void UpdateLODSettings()
        {
            if (_lodGroup == null)
                return;
                
            // Apply VR LOD bias to all LOD transitions
            LOD[] lods = _lodGroup.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                // Adjust the screen transition height based on the VR LOD bias
                // Negative bias increases quality by making transitions happen at smaller screen percentages
                float biasMultiplier = Mathf.Pow(2.0f, vrLODBias);
                
                // Apply dynamic LOD adjustment if enabled
                if (dynamicLOD)
                {
                    biasMultiplier *= _dynamicLODMultiplier;
                }
                
                // Adjust the screen relative transition height (clamped between 0.001 and 1)
                lods[i].screenRelativeTransitionHeight = Mathf.Clamp(
                    lods[i].screenRelativeTransitionHeight / biasMultiplier,
                    0.001f,
                    1.0f
                );
            }
            
            _lodGroup.SetLODs(lods);
        }
        
        private IEnumerator MonitorPerformance()
        {
            // Wait for a few frames to let everything initialize
            for (int i = 0; i < 10; i++)
                yield return null;
                
            while (autoOptimize && Application.isPlaying)
            {
                // Calculate frame time
                float currentFrameTime = Time.deltaTime;
                
                // Smooth the frame time readings
                if (_averageFrameTime == 0)
                    _averageFrameTime = currentFrameTime;
                else
                    _averageFrameTime = _averageFrameTime * _frameTimeSmoothing + currentFrameTime * (1.0f - _frameTimeSmoothing);
                
                // Calculate target frame time
                float targetFrameTime = 1.0f / targetFramerate;
                
                // Adjust LOD multiplier based on performance
                if (dynamicLOD)
                {
                    float frameTimeRatio = _averageFrameTime / targetFrameTime;
                    
                    // If we're over budget, reduce quality
                    if (frameTimeRatio > 1.05f)
                    {
                        _dynamicLODMultiplier = Mathf.Lerp(
                            _dynamicLODMultiplier,
                            Mathf.Min(_dynamicLODMultiplier + 0.5f, 1.0f + maxDynamicLODAdjustment),
                            _dynamicLODSpeed
                        );
                    }
                    // If we have headroom, increase quality
                    else if (frameTimeRatio < 0.85f)
                    {
                        _dynamicLODMultiplier = Mathf.Lerp(
                            _dynamicLODMultiplier,
                            Mathf.Max(_dynamicLODMultiplier - 0.2f, Mathf.Max(1.0f - maxDynamicLODAdjustment, 0.1f)),
                            _dynamicLODSpeed * 0.5f
                        );
                    }
                }
                
                yield return new WaitForSeconds(0.5f); // Check every half second
            }
        }
        
        /// <summary>
        /// Apply predefined settings based on selected quality preset
        /// </summary>
        public void ApplyQualityPreset()
        {
            switch (qualityPreset)
            {
                case VRQualityPreset.Minimum:
                    vrPointSizeScale = 0.3f;
                    distanceFade = 2.0f;
                    vrLODBias = 1.5f;
                    dynamicLOD = true;
                    maxDynamicLODAdjustment = 0.8f;
                    break;
                    
                case VRQualityPreset.Performance:
                    vrPointSizeScale = 0.6f;
                    distanceFade = 1.5f;
                    vrLODBias = 0.7f;
                    dynamicLOD = true;
                    maxDynamicLODAdjustment = 0.6f;
                    break;
                    
                case VRQualityPreset.Balanced:
                    vrPointSizeScale = 1.0f;
                    distanceFade = 1.0f;
                    vrLODBias = 0.0f;
                    dynamicLOD = true;
                    maxDynamicLODAdjustment = 0.5f;
                    break;
                    
                case VRQualityPreset.Quality:
                    vrPointSizeScale = 1.2f;
                    distanceFade = 0.7f;
                    vrLODBias = -0.5f;
                    dynamicLOD = true; 
                    maxDynamicLODAdjustment = 0.3f;
                    break;
                    
                case VRQualityPreset.Maximum:
                    vrPointSizeScale = 1.5f;
                    distanceFade = 0.3f;
                    vrLODBias = -1.0f;
                    dynamicLOD = false;
                    maxDynamicLODAdjustment = 0.2f;
                    break;
            }
            
            // Apply settings immediately
            UpdateShaderProperties();
            UpdateLODSettings();
        }
        
        // Display performance statistics in editor
        private void OnGUI()
        {
            if (showStats && Application.isPlaying)
            {
                GUI.Box(new Rect(10, 10, 220, 90), "Point Cloud VR Optimizer");
                
                GUI.Label(new Rect(20, 30, 200, 20), $"FPS: {(1.0f/_averageFrameTime):F1}");
                GUI.Label(new Rect(20, 50, 200, 20), $"Target: {targetFramerate} FPS");
                GUI.Label(new Rect(20, 70, 200, 20), $"LOD Multiplier: {_dynamicLODMultiplier:F2}x");
            }
        }
    }
}
