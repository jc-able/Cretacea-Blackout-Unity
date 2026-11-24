Shader "Cretacea/HDRP/CctvScreen"
{
    Properties
    {
        _MainTex ("CCTV Live RenderTexture", 2D) = "black" {}
        _Tint ("Monitor Phosphor Tint", Color) = (0.82, 0.96, 0.92, 1.0)
        _BlackoutStatic ("Blackout Static Blend (0=Live, 1=NoSignal)", Range(0.0, 1.0)) = 0.0
        _Power ("Screen Power (0=Off, 1=On)", Range(0.0, 1.0)) = 1.0
        _ScanlineDensity ("Scanline Density", Range(60.0, 480.0)) = 220.0
        _ScanlineStrength ("Scanline Strength", Range(0.0, 0.8)) = 0.28
        _ChromaticAberration ("Chromatic Aberration", Range(0.0, 0.015)) = 0.0025
        _VignetteStrength ("CRT Vignette Strength", Range(0.0, 1.0)) = 0.45
        _NoiseIntensity ("Live Signal Grain", Range(0.0, 0.4)) = 0.07
        _EmissionNits ("Display Brightness (Physical Scale)", Range(0.0, 80.0)) = 18.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float _BlackoutStatic;
                float _Power;
                float _ScanlineDensity;
                float _ScanlineStrength;
                float _ChromaticAberration;
                float _VignetteStrength;
                float _NoiseIntensity;
                float _EmissionNits;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                o.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                // Subtle CRT glass barrel curvature
                float2 centered = uv * 2.0 - 1.0;
                float r2 = dot(centered, centered);
                float2 crtUV = centered * (1.0 + 0.045 * r2) * 0.5 + 0.5;

                // Mask outside curved bezel
                float bezelMask = step(0.0, crtUV.x) * step(crtUV.x, 1.0) * step(0.0, crtUV.y) * step(crtUV.y, 1.0);

                // Subtle horizontal sync jitter on heavy static
                float frameTime = floor(_Time.y * 24.0);
                float lineJitter = (Hash21(float2(floor(crtUV.y * 90.0), frameTime)) - 0.5) * 0.03 * _BlackoutStatic;
                float2 sampleUV = crtUV + float2(lineJitter, 0.0);

                // Live feed with subtle RGB chromatic aberration
                float r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV + float2(_ChromaticAberration, 0.0)).r;
                float g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV).g;
                float b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV - float2(_ChromaticAberration, 0.0)).b;
                float3 liveCol = float3(r, g, b) * _Tint.rgb;

                // Subtle security camera green/cyan phosphor boost so dark monitors remain readable
                float liveGrain = (Hash21(floor(crtUV * float2(320.0, 180.0)) + frameTime) - 0.5) * _NoiseIntensity;
                liveCol = max(0.0, liveCol + liveGrain);

                // Blackout static snow + rolling tear bar + red warning status strip
                float2 staticCell = floor(crtUV * float2(260.0, 150.0));
                float snow = Hash21(staticCell + float2(frameTime * 1.7, frameTime * 3.1));
                float rollBand = smoothstep(0.18, 0.0, abs(frac(crtUV.y - _Time.y * 0.35) - 0.5));
                float3 staticCol = float3(0.75, 0.85, 0.92) * (snow * 0.65 + rollBand * 0.25);

                // Pulsing red "NO SIGNAL" indicator bar in center during blackout static
                float pulse = 0.5 + 0.5 * sin(_Time.y * 5.0);
                float barX = step(0.32, crtUV.x) * step(crtUV.x, 0.68);
                float barY = step(0.45, crtUV.y) * step(crtUV.y, 0.55);
                float border = barX * barY * (1.0 - (step(0.335, crtUV.x) * step(crtUV.x, 0.665) * step(0.468, crtUV.y) * step(crtUV.y, 0.532)));
                staticCol = lerp(staticCol, float3(0.95, 0.12, 0.08) * (0.45 + 0.55 * pulse), border * 0.85);

                float3 col = lerp(liveCol, staticCol, saturate(_BlackoutStatic));

                // Scanlines + rolling refresh bar + CRT vignette
                float scan = 1.0 - _ScanlineStrength * (0.5 + 0.5 * sin(crtUV.y * _ScanlineDensity * 3.14159265));
                float refresh = 1.0 + 0.08 * smoothstep(0.08, 0.0, abs(frac(crtUV.y + _Time.y * 0.18) - 0.5));
                float vig = saturate(1.0 - r2 * _VignetteStrength);

                col *= scan * refresh * vig * bezelMask * saturate(_Power);

                // Faint unpowered glass reflection floor when _Power == 0
                col += float3(0.008, 0.010, 0.012) * bezelMask * (1.0 - saturate(_Power));

                // Apply HDRP Pre-Exposure multiplier
                col *= _EmissionNits * GetCurrentExposureMultiplier();
                return float4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode" = "DepthForwardOnly" }

            Cull Back
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertDepth
            #pragma fragment FragDepth

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings VertDepth(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                return o;
            }
            void FragDepth(Varyings i) {}
            ENDHLSL
        }
    }
    FallBack Off
}
