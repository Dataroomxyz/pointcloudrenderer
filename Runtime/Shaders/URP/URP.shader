/*
Chris Nightingale, 2023

Note: Meshes should be in Point topology mode, or else some vertices may not be rendered

Stereo implementation from https://discussions.unity.com/t/geometry-shader-in-vr-stereo-rendering-mode-single-pass-instanced/231825/2
*/

Shader "StoryLab PointCloud/URP"
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

            struct v2g
            {
                float4 clipPos  : SV_POSITION;
                float4 color    : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct g2f
            {
                float4 clipPos  : SV_POSITION;
                float4 color    : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO

            #if !SOLID_COLOR
            #if FOG_LINEAR || FOG_EXP || FOG_EXP2
                float fogCoords : TEXCOORD1;
            #endif
            #endif
            };

        #if !SOLID_COLOR && !UNITY_COLORSPACE_GAMMA
            // assume that the vertex color is stored in sRGB space
            float3 GammaToLinearSpace(float3 sRGB)
            {
                // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
                return sRGB * (sRGB * (sRGB * 0.305306011f + 0.682171111f) + 0.012522878f);
            }
        #endif

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

            v2g vert(a2v v)
            {
                v2g o;
                ZERO_INITIALIZE(v2g, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.clipPos = TransformObjectToHClip(v.vertex.xyz);
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

                o.clipPos = vert;
                o.color = input[0].color;

                float radius = _PointSize * o.color.a * 12.75f;
            #if LOD_FADE_CROSSFADE
                radius *= GetEffectiveLODFactor();
            #endif

                float2 extent = abs(UNITY_MATRIX_P._11_22 * radius);
                // color alpha is used as a size multipler with a 10/255 being no change to give a range from 0.1 - 25.5x
                // radius is accounted for by 0.5, hence 10/255 * 0.5 = 12.75

            #if !SOLID_COLOR
            #if FOG_LINEAR || FOG_EXP || FOG_EXP2
                o.fogCoords = ComputeFogFactor(vert.z);
            #endif
            #endif

                // https://learn.microsoft.com/en-us/windows/win32/direct3d11/d3d10-graphics-programming-guide-primitive-topologies

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
                //#pragma exclude_renderers gles gles3 glcore 
                #pragma multi_compile_fog
                #pragma multi_compile _ UNITY_COLORSPACE_GAMMA
                #pragma multi_compile_instancing
                #pragma shader_feature _COLORMODE_VERTEX _COLORMODE_SOLID _COLORMODE_BLEND
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
                //#pragma exclude_renderers gles gles3 glcore 
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
    Fallback "Hidden/StoryLab PointCloud/URP_Point"
    CustomEditor "StoryLabResearch.PointCloud.PointCloudShaderGUI"
}