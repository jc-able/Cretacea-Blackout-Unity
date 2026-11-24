Shader "Cretacea/HDRP/WetPuddleOverlay"
{
    Properties
    {
        _PuddleMask ("Puddle Mask (R=Puddle)", 2D) = "white" {}
        _RainRipplesNormal ("Rain Ripples Flipbook Normal (Optional)", 2D) = "bump" {}
        _UseRippleTex ("Use External Ripple Flipbook (0=Procedural Rings, 1=Texture)", Range(0.0, 1.0)) = 0.0
        _FlipbookGrid ("Ripple Flipbook Cols/Rows", Vector) = (4, 4, 14, 0)
        _RippleTiling ("World Ripple Tiling (m)", Range(0.2, 8.0)) = 1.6
        _PuddleTiling ("World Puddle Mask Tiling", Range(0.01, 2.0)) = 0.12
        _PuddleDarken ("Wet Asphalt Darken Tint", Color) = (0.04, 0.05, 0.06, 0.72)
        _ReflectionColor ("Sky/Storm Specular Tint", Color) = (0.32, 0.44, 0.58, 1.0)
        _ReflectionIntensity ("Puddle Reflection Strength", Range(0.0, 6.0)) = 1.6
        _DryInteriorMask ("Force Dry (0=Auto Roof Occluded, 1=Always Dry)", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-50"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_PuddleMask);
            SAMPLER(sampler_PuddleMask);

            TEXTURE2D(_RainRipplesNormal);
            SAMPLER(sampler_RainRipplesNormal);

            TEXTURE2D(_CretaceaRainOcclusionTex);
            SAMPLER(sampler_CretaceaRainOcclusionTex);
            float4 _CretaceaRainOcclusionParams;
            float  _GlobalWetness;
            float  _RainIntensity;

            CBUFFER_START(UnityPerMaterial)
                float4 _FlipbookGrid;
                float4 _PuddleDarken;
                float4 _ReflectionColor;
                float _UseRippleTex;
                float _RippleTiling;
                float _PuddleTiling;
                float _ReflectionIntensity;
                float _DryInteriorMask;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            // Hash22 from "Hash without Sine", Copyright (c) 2014 David Hoskins, MIT License, https://www.shadertoy.com/view/4djSRW
            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // Crisp procedural expanding raindrop ripple rings (fallback before rain_ripples_normal.png is assigned)
            float2 ProceduralRippleSlope(float2 worldXZ, float t)
            {
                float2 uv = worldXZ * _RippleTiling;
                float2 cell = floor(uv);
                float2 f = frac(uv);
                float2 slope = float2(0.0, 0.0);

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 c = cell + float2(x, y);
                        float2 rnd = Hash22(c);
                        float phase = frac(t * 0.85 + rnd.x * 7.13 + rnd.y * 3.71);
                        float2 center = float2(x, y) + 0.25 + 0.5 * rnd;
                        float2 d = f - center;
                        float dist = length(d);
                        float ring = sin((dist - phase * 0.65) * 42.0) * exp(-abs(dist - phase * 0.65) * 14.0) * (1.0 - phase);
                        slope += (d / max(dist, 0.001)) * ring;
                    }
                }
                return slope * 0.35;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 posRWS = TransformObjectToWorld(input.positionOS);
                o.positionWS  = GetAbsolutePositionWS(posRWS);
                o.positionCS  = TransformWorldToHClip(posRWS);
                o.normalWS    = normalize(TransformObjectToWorldNormal(input.normalOS));
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                if (_DryInteriorMask > 0.99 || _GlobalWetness < 0.01)
                    discard;

                // Roof occlusion check: keep covered interiors dry
                float outdoorFactor = 1.0;
                if (_CretaceaRainOcclusionParams.z > 0.5)
                {
                    float2 rel = (i.positionWS.xz - _CretaceaRainOcclusionParams.xy) / (_CretaceaRainOcclusionParams.z * 2.0) + 0.5;
                    if (rel.x >= 0.0 && rel.x <= 1.0 && rel.y >= 0.0 && rel.y <= 1.0)
                    {
                        float roofY = SAMPLE_TEXTURE2D(_CretaceaRainOcclusionTex, sampler_CretaceaRainOcclusionTex, rel).r;
                        if (roofY > -5000.0 && i.positionWS.y < (roofY - 0.15))
                            outdoorFactor = 0.0;
                    }
                }

                float maskTex = SAMPLE_TEXTURE2D(_PuddleMask, sampler_PuddleMask, i.positionWS.xz * _PuddleTiling).r;
                float wetness = saturate(_GlobalWetness * (1.0 - _DryInteriorMask) * outdoorFactor);
                float puddle = smoothstep(0.25, 0.80, maskTex) * wetness;
                if (puddle < 0.01)
                    discard;

                // Compute animated ripple normal perturbation
                float2 rippleSlope = ProceduralRippleSlope(i.positionWS.xz, _Time.y) * saturate(_RainIntensity);
                if (_UseRippleTex > 0.5)
                {
                    float cols = max(1.0, _FlipbookGrid.x);
                    float rows = max(1.0, _FlipbookGrid.y);
                    float fps  = max(1.0, _FlipbookGrid.z);
                    float total = cols * rows;
                    float frame = floor(fmod(_Time.y * fps, total));
                    float colIdx = fmod(frame, cols);
                    float rowIdx = floor(frame / cols);
                    float2 tileUV = (frac(i.positionWS.xz * _RippleTiling) + float2(colIdx, rows - 1.0 - rowIdx)) / float2(cols, rows);
                    float3 nTex = UnpackNormal(SAMPLE_TEXTURE2D(_RainRipplesNormal, sampler_RainRipplesNormal, tileUV));
                    rippleSlope = nTex.xy * 0.45 * saturate(_RainIntensity);
                }

                float3 N = normalize(float3(i.normalWS.x + rippleSlope.x, max(0.2, i.normalWS.y), i.normalWS.z + rippleSlope.y));
                float3 camWS = GetAbsolutePositionWS(float3(0.0, 0.0, 0.0));
                float3 V = normalize(camWS - i.positionWS);
                float NdotV = saturate(dot(N, V));
                float fresnel = 0.04 + 0.96 * pow(1.0 - NdotV, 4.5);

                // Dark wet asphalt base + specular Fresnel sky/ripple glint
                float rippleGlint = saturate(length(rippleSlope) * 2.5);
                float3 col = _PuddleDarken.rgb + _ReflectionColor.rgb * (fresnel * _ReflectionIntensity + rippleGlint * 0.65);
                col *= GetCurrentExposureMultiplier();

                float alpha = saturate(puddle * (_PuddleDarken.a * 0.55 + fresnel * 0.45));
                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
