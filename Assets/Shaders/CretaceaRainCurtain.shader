Shader "Cretacea/HDRP/RainCurtain"
{
    // Distant rain: curtains of falling water seen 50-150 m away (two camera-centred rings built by
    // StormRainOcclusionController). They veil whatever lies behind them, so the far hills and tree lines stand as
    // silhouettes in a grey-blue haze, and they light up when lightning flashes (the sky reveals the rain sheets).
    //
    // Lighting uses the weather globals of WeatherLightSampler (lux): sky ambient + moon + lightning, plus the
    // distant cloud flicker of LightningBoltController (_CretaceaSkyFlicker, lux). Radiance = lux x scatter, nits,
    // pre-exposed. Depth-faded against the scene, so a curtain never cuts a hard line across a hill.
    //   mesh uv.x = arc length (m, scaled so the ring holds a whole number of streak cells), uv.y = height above the
    //   ring's base (m), uv2.x = ring index (0 near, 1 mid, 2 far), uv2.y = streak lattice period around the ring.
    //   Every noise lattice wraps with its period, so the ring closes without a seam (fix round: a step due north).
    Properties
    {
        _Color ("Tint", Color) = (0.78, 0.84, 0.95, 1)
        _Opacity ("Opacity scale at full rain", Range(0, 2)) = 1.0
        _RingOpacity ("Ring opacity near / mid / far", Vector) = (0.18, 0.35, 0.5, 0)
        _Scatter ("Scatter (nits per lux)", Range(0, 0.3)) = 0.06
        _MoonWeight ("Moon weight", Range(0, 1)) = 0.6
        _FlashWeight ("Lightning weight", Range(0, 2)) = 1.0
        _DepthFade ("Depth fade distance (m)", Float) = 18
        _Height ("Ring height (m)", Float) = 120
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-50" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"

            float  _RainIntensity;
            float4 _CretaceaWind;          // xyz m/s, w = speed
            float4 _CretaceaWxDirColor0;   // moon (lux)
            float4 _CretaceaWxDirColor1;   // lightning (lux)
            float4 _CretaceaWxAmbient;     // sky (lux)
            float  _CretaceaSkyFlicker;    // distant cloud flicker (lux)

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Opacity;
                float4 _RingOpacity;
                float _Scatter;
                float _MoonWeight;
                float _FlashWeight;
                float _DepthFade;
                float _Height;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uvr        : TEXCOORD0;   // arc m, height m, ring, streak period
                float3 positionRWS: TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 rws = TransformObjectToWorld(v.positionOS);
                o.positionRWS = rws;
                o.positionCS = TransformWorldToHClip(rws);
                o.uvr = float4(v.uv, v.uv2.x, max(v.uv2.y, 1.0));
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 WrapCell(float2 c, float2 period) { return c - period * floor(c / period); }

            // value noise whose lattice repeats every `period` cells (x around the ring, y in time)
            float VNoiseP(float2 p, float2 period)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float2 i0 = WrapCell(i, period), i1 = WrapCell(i + 1.0, period);
                float a = Hash21(i0), b = Hash21(float2(i1.x, i0.y)), c = Hash21(float2(i0.x, i1.y)), d = Hash21(i1);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float rain = saturate(_RainIntensity);
                if (rain < 0.02) discard;

                float t = _Time.y;
                float ring = i.uvr.z;
                float arc = i.uvr.x, h = i.uvr.y;
                float period = i.uvr.w;                                  // streak cells around this ring (integer)
                float gustPeriod = max(4.0, round(period * 0.0075));    // ~ circumference x 0.018
                float slant = clamp(_CretaceaWind.w / 9.0, 0.0, 0.7) * 0.6;
                float fall = 9.0;
                const float YP = 1024.0;                                 // time lattice period (keeps the hash precise)

                // streaks: thin vertical threads (about 0.4 m wide, 6-10 m long at this range), falling
                float scroll = fmod(t * fall * 0.11, YP);
                float2 sp = float2((arc + h * slant) * 2.4, h * 0.11 + scroll + ring * 17.0);
                float s1 = VNoiseP(sp, float2(period, YP));
                // second octave: scaled by whole numbers so it repeats around the ring and in time too
                float s2 = VNoiseP(sp * 2.0 + 7.0, float2(period * 2.0, YP * 2.0));
                float streak = saturate(pow(s1 * 0.65 + s2 * 0.35, 3.0) * 2.4);

                // gusts: broad bands of heavier rain drifting with the wind (a whole number of bands around the ring)
                float gx = arc * 2.4 / period * gustPeriod - fmod(t * 0.05 * (1.0 + ring), gustPeriod);
                float g = VNoiseP(float2(gx, h * 0.008 + ring * 3.0), float2(gustPeriod, YP));
                float gust = smoothstep(0.25, 0.85, g);

                // fade at the bottom (below the ground) and the top of the ring
                float hf = saturate(h / 12.0) * saturate((_Height - h) / (_Height * 0.45));

                // soft against the scene: only where the scene is well behind the curtain (or the sky)
                float sceneDepth = LinearEyeDepth(LoadCameraDepth(uint2(i.positionCS.xy)), _ZBufferParams);
                float myDepth = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float df = saturate((sceneDepth - myDepth) / max(_DepthFade, 0.1));

                // light rain (dusk 0.25, night 0.45) barely forms curtains; the storm does
                float ringOpacity = ring < 0.5 ? _RingOpacity.x : (ring < 1.5 ? _RingOpacity.y : _RingOpacity.z);
                float alpha = _Opacity * ringOpacity * rain * sqrt(rain) * (0.3 + 0.7 * gust) * (0.6 + 0.4 * streak) * hf * df;
                if (alpha < 0.001) discard;

                float3 L = (_CretaceaWxAmbient.rgb + _CretaceaWxDirColor0.rgb * _MoonWeight + _CretaceaWxDirColor1.rgb * _FlashWeight
                            + float3(0.75, 0.82, 1.0) * _CretaceaSkyFlicker) * _Scatter * _Color.rgb;
                // a veil of rain is never brighter than about half the scene's white (a soft shoulder in exposed units):
                // at dusk the sky light is thousands of lux and the raw value would whiten the whole hill
                float3 Le = L * GetCurrentExposureMultiplier();
                Le *= 1.0 / (1.0 + Luminance(Le) / 0.5);
                float3 rgb = Le * alpha;

                PositionInputs posInput = GetPositionInput(i.positionCS.xy, _ScreenSize.zw, i.positionCS.z, i.positionCS.w, i.positionRWS);
                float3 V = normalize(-i.positionRWS);
                float3 fogColor, fogOpacity;
                EvaluateAtmosphericScattering(posInput, V, fogColor, fogOpacity);
                float keep = 1.0 - Luminance(fogOpacity) * 0.7;
                return float4(rgb * keep, alpha * keep);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
