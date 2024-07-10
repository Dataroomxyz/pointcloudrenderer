/*
Chris Nightingale, 2023

Note: Meshes should be in Point topology mode, or else some vertices may not be rendered

Stereo implementation from https://discussions.unity.com/t/geometry-shader-in-vr-stereo-rendering-mode-single-pass-instanced/231825/2
*/

Shader "Hidden/StoryLab PointCloud/URP_Point"
{
    Properties
    {
        _PointSize("Point Size", Float) = 0.02
        [KeywordEnum(Vertex, Solid, Blend)] _ColorMode("Color Mode", int) = 0
        _Color("Color", Color) = (1,1,1,1)
        _ColorBlend("Color Blend", Range(0,1)) = 0
        // note to self: Even if we are using solid color, we still need to pass the color data through because the shader uses the alpha for scale
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

            static const float AREA_COMPENSATION = 0.6;
            // make area match the area of the diamond points

            CBUFFER_START(UnityPerMaterial)
                float _PointSize;
                half4 _Color;
                float _ColorBlend;
            CBUFFER_END

            struct a2v
            {
                float3 vertex   : POSITION;
                float4 color    : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 clipPos  : SV_POSITION;
                float4 color    : COLOR;
                float size      : PSIZE;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO

            #if FOG_LINEAR || FOG_EXP || FOG_EXP2
                float fogCoords : TEXCOORD1;
            #endif
            };

            // assume that the vertex color is stored in sRGB space
            float3 GammaToLinearSpace(float3 sRGB)
            {
                // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
                return sRGB * (sRGB * (sRGB * 0.305306011f + 0.682171111f) + 0.012522878f);
            }

        #if LOD_FADE_CROSSFADE
            float GetAbsoluteLODFactor()
            {
                if (unity_LODFade.x >= 0) return unity_LODFade.x;
                else return 1 + unity_LODFade.x;
            }

            float GetEffectiveLODFactor()
            {
                return saturate(GetAbsoluteLODFactor() * 1.5);
            }
        #endif

            v2f vert(a2v v)
            {
                v2f o;
                ZERO_INITIALIZE(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.clipPos = TransformObjectToHClip(v.vertex.xyz);
                o.color = v.color;
            #if FOG_LINEAR || FOG_EXP || FOG_EXP2
                o.fogCoords = ComputeFogFactor(vert.z);
            #endif

                float pointSize = _PointSize;
            #if LOD_FADE_CROSSFADE
                pointSize *= GetEffectiveLODFactor();
            #endif

                o.size = AREA_COMPENSATION * pointSize * 255 * o.color.a / o.clipPos.w * _ScreenParams.y;
                // color alpha (8-bit uint, 0-255, represented as float 0-1) is used as a size multipler
                return o;
            }

            float3 frag(v2f m) : SV_TARGET
            {

                #if LOD_FADE_CROSSFADE && DEBUG
                    return half3(1, 1, 0);
                #else
                    #if _COLORMODE_SOLID
                        return _Color;
                    #else
                        #if _COLORMODE_BLEND
                            half3 outColor = ((1 - _ColorBlend) * m.color.rgb) + (_ColorBlend * _Color.rgb);
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
                                return GammaToLinearSpace(MixFog(outColor, m.fogCoords));
                            #else
                                return GammaToLinearSpace(outColor);
                            #endif
                        #endif
                    #endif
                #endif
            }

            float3 depthfrag(v2f m) : SV_TARGET
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
                #pragma target 2.5
                #pragma multi_compile_fog
                #pragma multi_compile _ UNITY_COLORSPACE_GAMMA
                #pragma multi_compile_instancing
                #pragma multi_compile _ SOLID_COLOR
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma multi_compile _ DEBUG
                #pragma vertex vert
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
                #pragma target 2.5
                #pragma multi_compile_instancing
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma vertex vert
                #pragma fragment depthfrag
            ENDHLSL
        }
    }
    CustomEditor "StoryLabResearch.PointCloud.PointCloudShaderGUI"

}