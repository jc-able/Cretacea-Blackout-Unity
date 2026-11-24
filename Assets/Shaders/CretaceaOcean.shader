Shader "Cretacea/HDRP/DawnOcean"
{
    Properties
    {
        _DeepColor ("Deep Ocean Color", Color) = (0.025, 0.095, 0.145, 1.0)
        _ShallowColor ("Shallow Coastal Color", Color) = (0.075, 0.28, 0.32, 1.0)
        _HorizonSkyColor ("Sunrise Sky Reflection", Color) = (0.95, 0.62, 0.36, 1.0)
        _ZenithSkyColor ("Zenith Sky Reflection", Color) = (0.14, 0.24, 0.42, 1.0)
        _FoamColor ("Shoreline Foam Color", Color) = (0.92, 0.90, 0.86, 1.0)

        [Header(Sun Specular)]
        _SunDirection ("Sun Direction (World, To Sun)", Vector) = (0.92, 0.14, -0.35, 0.0)
        _SunColor ("Sunrise Sun Color", Color) = (1.0, 0.72, 0.42, 1.0)
        _SunIntensity ("Sun Specular Intensity", Range(0.0, 40.0)) = 12.0
        _Roughness ("Surface Roughness", Range(0.02, 0.6)) = 0.09

        [Header(Gerstner Waves)]
        _WaveAmplitude ("Wave Amplitude", Range(0.0, 2.5)) = 0.55
        _WaveSpeed ("Wave Speed", Range(0.0, 4.0)) = 1.15
        _WaveFrequency ("Wave Frequency", Range(0.01, 0.5)) = 0.085
        _DetailScale ("Micro Ripple Scale", Range(0.05, 2.0)) = 0.38
        _DetailStrength ("Micro Ripple Strength", Range(0.0, 1.0)) = 0.32

        [Header(Shoreline Foam and Atmospheric Haze)]
        _FoamDistance ("Foam Depth Distance", Range(0.2, 12.0)) = 3.2
        _FoamIntensity ("Foam Intensity", Range(0.0, 2.0)) = 0.95
        _OceanHazeColor ("Horizon Haze Color", Color) = (0.85, 0.64, 0.46, 1.0)
        _HazeStart ("Horizon Haze Start (m)", Float) = 160.0
        _HazeEnd ("Horizon Haze End (m)", Float) = 900.0
        _LuminanceScale ("Physical Radiance Scale (Dawn EV)", Float) = 1.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _HorizonSkyColor;
                float4 _ZenithSkyColor;
                float4 _FoamColor;
                float4 _SunDirection;
                float4 _SunColor;
                float4 _OceanHazeColor;
                float _SunIntensity;
                float _Roughness;
                float _WaveAmplitude;
                float _WaveSpeed;
                float _WaveFrequency;
                float _DetailScale;
                float _DetailStrength;
                float _FoamDistance;
                float _FoamIntensity;
                float _HazeStart;
                float _HazeEnd;
                float _LuminanceScale;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  waveCrest   : TEXCOORD2;
                float  viewDepth   : TEXCOORD3;
            };

            // Evaluate 4-octave Gerstner wave sum (displacement + analytical normal)
            void EvaluateGerstner(float2 xz, float t, out float3 disp, out float3 normalWS, out float crest)
            {
                static const float4 kWaves[4] =
                {
                    float4( 0.85,  0.52, 0.45, 1.00),
                    float4( 0.42, -0.90, 0.30, 1.65),
                    float4(-0.68,  0.73, 0.18, 2.60),
                    float4( 0.96, -0.25, 0.10, 4.10)
                };

                disp = float3(0.0, 0.0, 0.0);
                float3 tangent = float3(1.0, 0.0, 0.0);
                float3 binormal = float3(0.0, 0.0, 1.0);
                crest = 0.0;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 dir = normalize(kWaves[i].xy);
                    float steep = kWaves[i].z * 0.45;
                    float k = _WaveFrequency * kWaves[i].w;
                    float amp = _WaveAmplitude * kWaves[i].z;
                    float speed = sqrt(9.8 * k) * _WaveSpeed;
                    float phase = dot(dir, xz) * k + t * speed;

                    float s, c;
                    sincos(phase, s, c);

                    disp.x += dir.x * (amp * steep * c);
                    disp.z += dir.y * (amp * steep * c);
                    disp.y += amp * s;
                    crest += saturate(s) * kWaves[i].z;

                    float wa = k * amp;
                    tangent  += float3(-dir.x * dir.x * steep * wa * s, dir.x * wa * c, -dir.x * dir.y * steep * wa * s);
                    binormal += float3(-dir.x * dir.y * steep * wa * s, dir.y * wa * c, -dir.y * dir.y * steep * wa * s);
                }

                normalWS = normalize(cross(binormal, tangent));
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float3 MicroWaveNormal(float2 xz, float t)
            {
                float2 uv1 = xz * _DetailScale + float2(t * 0.35, -t * 0.22);
                float2 uv2 = xz * (_DetailScale * 1.93) + float2(-t * 0.28, t * 0.41);
                float eps = 0.08;
                float h0 = ValueNoise(uv1) * 0.6 + ValueNoise(uv2) * 0.4;
                float hx = ValueNoise(uv1 + float2(eps, 0)) * 0.6 + ValueNoise(uv2 + float2(eps, 0)) * 0.4;
                float hz = ValueNoise(uv1 + float2(0, eps)) * 0.6 + ValueNoise(uv2 + float2(0, eps)) * 0.4;
                float3 n = normalize(float3((h0 - hx) * _DetailStrength * 3.5, 1.0, (h0 - hz) * _DetailStrength * 3.5));
                return n;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 baseRWS = TransformObjectToWorld(input.positionOS);
                float3 baseWS  = GetAbsolutePositionWS(baseRWS);

                float3 disp, nrm;
                float crest;
                EvaluateGerstner(baseWS.xz, _Time.y, disp, nrm, crest);

                float3 displacedWS  = baseWS + disp;
                float3 displacedRWS = GetCameraRelativePositionWS(displacedWS);

                o.positionCS = TransformWorldToHClip(displacedRWS);
                o.positionWS = displacedWS;
                o.normalWS   = nrm;
                o.waveCrest  = crest;
                o.viewDepth  = -TransformWorldToView(displacedRWS).z;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 camWS = GetAbsolutePositionWS(float3(0.0, 0.0, 0.0));
                float3 viewVec = camWS - i.positionWS;
                float dist = length(viewVec);
                float3 V = viewVec / max(dist, 0.0001);

                // Blend macro Gerstner normal + micro wind ripples (fade micro noise over distance)
                float detailFade = saturate(1.0 - dist / 240.0);
                float3 microN = MicroWaveNormal(i.positionWS.xz, _Time.y);
                float3 N = normalize(lerp(i.normalWS, normalize(float3(i.normalWS.x + microN.x, i.normalWS.y, i.normalWS.z + microN.z)), detailFade));

                float3 L = normalize(_SunDirection.xyz);
                float3 H = normalize(L + V);
                float NdotV = saturate(dot(N, V));
                float NdotL = saturate(dot(N, L));
                float NdotH = saturate(dot(N, H));

                // Physical water Fresnel (Schlick F0 = 0.02)
                float fresnel = 0.02 + 0.98 * pow(1.0 - NdotV, 5.0);

                // Sky reflection gradient (warm dawn horizon -> cooler zenith)
                float3 R = reflect(-V, N);
                float skyElev = saturate(R.y);
                float3 skyRefl = lerp(_HorizonSkyColor.rgb, _ZenithSkyColor.rgb, pow(skyElev, 0.55));

                // Shallow scatter on wave crests + deep ocean body
                float scatter = saturate(pow(saturate(dot(V, -L)), 3.0) * 0.6 + i.waveCrest * 0.45);
                float3 waterBody = lerp(_DeepColor.rgb, _ShallowColor.rgb, scatter * 0.75 + (1.0 - NdotV) * 0.25);
                waterBody *= (0.35 + 0.65 * NdotL);

                // Crisp GGX sun glitter path across the sunrise ocean
                float a = max(0.02, _Roughness * _Roughness);
                float a2 = a * a;
                float denom = (NdotH * NdotH) * (a2 - 1.0) + 1.0;
                float ggxD = a2 / max(3.14159265 * denom * denom, 0.0001);
                float3 sunSpec = _SunColor.rgb * (ggxD * _SunIntensity * (0.25 + 0.75 * fresnel) * saturate(L.y * 5.0 + 0.2));

                // Combine reflection + body + specular
                float3 col = lerp(waterBody, skyRefl, saturate(fresnel * 1.15)) + sunSpec;

                // Subtle whitecap crest foam + procedural shoreline surf ripple
                float foamNoise = ValueNoise(i.positionWS.xz * 0.55 + float2(_Time.y * 0.4, -_Time.y * 0.25));
                float crestFoam = smoothstep(0.68, 0.95, i.waveCrest + foamNoise * 0.25) * 0.35;

                // Scene depth shoreline contact foam (only where solid cliff/terrain depth exists behind ocean!)
                float2 screenUV = i.positionCS.xy * _ScreenSize.zw;
                float rawSceneDepth = SampleCameraDepth(screenUV);
                float shoreMask = 0.0;
                float waterThickness = _FoamDistance;
                // Reversed-Z in HDRP: far plane is 0.0, near plane is 1.0; ignore sky (rawSceneDepth <= 0.0001)
                if (rawSceneDepth > 0.0001)
                {
                    float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                    waterThickness = max(0.0, sceneEyeDepth - i.viewDepth);
                    shoreMask = 1.0 - saturate(waterThickness / max(0.2, _FoamDistance));
                }
                float shoreWave = smoothstep(0.32, 0.78, shoreMask + sin(waterThickness * 4.5 - _Time.y * 2.4) * 0.14 + foamNoise * 0.25);

                float totalFoam = saturate((crestFoam + shoreWave * shoreMask) * _FoamIntensity);
                col = lerp(col, _FoamColor.rgb * (0.65 + 0.35 * _SunColor.rgb), totalFoam);

                // Horizon atmospheric dawn mist blend
                float fogFactor = saturate((dist - _HazeStart) / max(1.0, _HazeEnd - _HazeStart));
                col = lerp(col, _OceanHazeColor.rgb, fogFactor * fogFactor);

                // Apply HDRP Pre-Exposure multiplier
                col *= _LuminanceScale * GetCurrentExposureMultiplier();
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
