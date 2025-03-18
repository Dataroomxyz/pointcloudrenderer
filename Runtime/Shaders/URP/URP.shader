/*
Chris Nightingale, 2023
Updated for Unity 6 compatibility by DataRoomXYZ, 2025

Note: Meshes should be in Point topology mode, or else some vertices may not be rendered
*/

Shader "StoryLab PointCloud/URP"
{
    Properties
    {
        _PointSize("Point Size", Float) = 0.02
        [KeywordEnum(Vertex, Solid, Blend)] _ColorMode("Color Mode", int) = 0
        _Color("Color", Color) = (1,1,1,1)
        _ColorBlend("Color Blend", Range(0,1)) = 0
        _VRScale("VR Scale Multiplier", Range(0.1,3.0)) = 1.0
        _DistanceFade("Distance Fade", Range(0,1)) = 0.5
        [Toggle(DEBUG)] _Debug("Debug Crossfade", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        Cull Off

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _PointSize;
                half4 _Color;
                float _ColorBlend;
                float _VRScale;
                float _DistanceFade;
            CBUFFER_END

            struct a2v
            {
                float3 vertex   : POSITION;
                float4 color    : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2g
            {
                float4 clipPos  : SV_POSITION;
                float4 color    : COLOR;
                float4 worldPos : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct g2f
            {
                float4 clipPos  : SV_POSITION;
                float4 color    : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO

            #if FOG_LINEAR || FOG_EXP || FOG_EXP2
                float fogCoords : TEXCOORD1;
            #endif
            };

            // More efficient gamma to linear conversion for Unity 6
            float3 GammaToLinearSpaceOptimized(float3 sRGB)
            {
                return sRGB * (sRGB * (sRGB * 0.305306011f + 0.682171111f) + 0.012522878f);
            }

        #if LOD_FADE_CROSSFADE
            float GetEffectiveLODFactor()
            {
                float factor = unity_LODFade.x;
                if (factor < 0) factor = 1 + factor;
                return saturate(factor * 1.5);
            }
        #endif

            v2g vert(a2v v)
            {
                v2g o;
                ZERO_INITIALIZE(v2g, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.clipPos = TransformObjectToHClip(v.vertex.xyz);
                o.worldPos = mul(UNITY_MATRIX_M, float4(v.vertex.xyz, 1.0));
                o.color = v.color;
                return o;
            }

            [maxvertexcount(4)]
            void geom(point v2g input[1], inout TriangleStream<g2f> triStream)
            {
                g2f o;
                ZERO_INITIALIZE(g2f, o);
                UNITY_SETUP_INSTANCE_ID(input[0]);
                UNITY_TRANSFER_INSTANCE_ID(input[0], o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 vert = input[0].clipPos;
                float4 worldPos = input[0].worldPos;

                o.clipPos = vert;
                o.color = input[0].color;

                // Calculate distance scale factor (smaller points when far away)
                float distanceToCamera = length(_WorldSpaceCameraPos.xyz - worldPos.xyz);
                float distanceScale = 1.0 / max(1.0, distanceToCamera * _DistanceFade);
                
                // Calculate scaled point size
                float radius = _PointSize * _VRScale * o.color.a * 255.0 * 0.5 * distanceScale;

            #if LOD_FADE_CROSSFADE
                radius *= GetEffectiveLODFactor();
            #endif

                float2 extent = abs(UNITY_MATRIX_P._11_22 * radius);

            #if FOG_LINEAR || FOG_EXP || FOG_EXP2
                o.fogCoords = ComputeFogFactor(vert.z);
            #endif

                // Triangle strips version
                // Top vertex
                o.clipPos.y = vert.y + extent.y;
                triStream.Append(o);
                // Left side vertex
                o.clipPos.x = vert.x - extent.x;
                o.clipPos.y = vert.y;
                triStream.Append(o);
                // Right side vertex
                o.clipPos.x = vert.x + extent.x;
                o.clipPos.y = vert.y;
                triStream.Append(o);
                // Bottom vertex
                o.clipPos.x = vert.x;
                o.clipPos.y = vert.y - extent.y;
                triStream.Append(o);

                triStream.RestartStrip();
            }

            float3 frag(g2f m) : SV_TARGET
            {

                #if LOD_FADE_CROSSFADE && DEBUG
                    return half3(1, 1, 0);
                #else
                    #if _COLORMODE_SOLID
                        return _Color.rgb;
                    #else
                        #if _COLORMODE_BLEND
                            half3 outColor = lerp(m.color.rgb, _Color.rgb, _ColorBlend);
                        #else
                            half3 outColor = m.color.rgb;
                        #endif
                        #ifdef UNITY_COLORSPACE_GAMMA
                            #if FOG_LINEAR || FOG_EXP || FOG_EXP2
                                return MixFog(outColor, m.fogCoords);
                            #else
                                return outColor;
                            #endif
                        #else
                            #if FOG_LINEAR || FOG_EXP || FOG_EXP2
                                return GammaToLinearSpaceOptimized(MixFog(outColor, m.fogCoords));
                            #else
                                return GammaToLinearSpaceOptimized(outColor);
                            #endif
                        #endif
                    #endif
                #endif
            }

            float3 depthfrag(g2f m) : SV_TARGET
            {
                return 0;
            }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags
            {
                "LightMode" = "UniversalForward"
                "RenderType" = "Opaque"
                "UniversalMaterialType" = "Unlit"
                "Queue" = "Geometry"
            }

            HLSLPROGRAM
                #pragma target 4.5
                #pragma multi_compile_fog
                #pragma multi_compile _ UNITY_COLORSPACE_GAMMA
                #pragma multi_compile_instancing
                #pragma shader_feature_local _COLORMODE_VERTEX _COLORMODE_SOLID _COLORMODE_BLEND
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma multi_compile _ DEBUG
                #pragma require geometry
                #pragma vertex vert
                #pragma geometry geom
                #pragma fragment frag
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
                "RenderType" = "Opaque"
                "UniversalMaterialType" = "Unlit"
                "Queue" = "Geometry"
            }

            HLSLPROGRAM
                #pragma target 4.5
                #pragma multi_compile _ UNITY_COLORSPACE_GAMMA
                #pragma multi_compile_instancing
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma require geometry
                #pragma vertex vert
                #pragma geometry geom
                #pragma fragment depthfrag
            ENDHLSL
        }
    }
    Fallback "StoryLab PointCloud/URP_Mobile"
    CustomEditor "StoryLabResearch.PointCloud.PointCloudShaderGUI"
}
