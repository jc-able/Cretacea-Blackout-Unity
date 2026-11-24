Shader "Cretacea/HDRP/RainParticle"
{
    // Physically lit weather particles for HDRP (rain streaks, ground ripples, drips, dust motes, sparks).
    //
    // The particle is not an emitter: it reflects the light that reaches it. WeatherLightSampler publishes the
    // nearest lamps (candela, cone, range), the flashlight, the moon / lightning directional lights (lux) and a
    // sky ambient (lux) as shader globals. Radiance = illuminance x scatter x phase, in nits, then pre-exposed with
    // HDRP's exposure multiplier and attenuated by HDRP fog, so a streak is bright in a lamp's cone, a silver sheet
    // during a lightning strike, faint blue in moonlight, invisible in the dark and gone in the fog. Every light's
    // contribution is capped below that lamp's own diffuser luminance.
    //
    // Particle vertex streams (set by StormRainOcclusionController / the weather scripts):
    //   POSITION, COLOR, TEXCOORD0.xy = UV, TEXCOORD0.z = AgePercent, TEXCOORD0.w = StableRandom.x
    Properties
    {
        _MainTex ("Optional Texture (alpha)", 2D) = "white" {}
        _UseTexture ("Use Texture (0 = procedural shape)", Range(0.0, 1.0)) = 0.0
        [Enum(Streak,0,Ripple,1,Mote,2,Spark,3)] _Mode ("Mode", Float) = 0
        _IsSplash ("Legacy splash flag (1 = ripple)", Range(0.0, 1.0)) = 0.0
        _Color ("Tint (rgb) / Opacity (a)", Color) = (0.86, 0.92, 1.0, 0.42)
        _Scatter ("Scatter (radiance per lux)", Range(0.0, 0.5)) = 0.07
        _Brightness ("Legacy brightness (multiplies scatter)", Range(0.05, 10.0)) = 1.0
        _Softness ("Edge Softness", Range(0.1, 4.0)) = 1.5
        _StreakAxis ("Streak long axis (0 = V, 1 = U)", Range(0.0, 1.0)) = 0.0
        _Occlusion ("Background occlusion", Range(0.0, 1.0)) = 0.12
        _EmissionNits ("Self emission (sparks), nits", Float) = 0
        _MaxNits ("Luminance cap, nits", Float) = 110
        _ExposedCap ("Cap floor as a fraction of scene white (daylight rain)", Float) = 0
        _NearFade ("Near fade start / end (m)", Vector) = (0.35, 1.4, 0, 0)
        _SkyLight ("Moon / sky weight", Range(0.0, 1.0)) = 1.0
        _FlashLight ("Lightning weight", Range(0.0, 1.0)) = 1.0
        _IndoorLights ("Indoor lamp weight (0 rain, 1 dust)", Range(0.0, 1.0)) = 0.0
        _CoverageDistance ("Streak: distance (m) up to which a streak covers its whole pixel", Float) = 14.0
        _UseRoofOcclusion ("Discard under roofs", Range(0.0, 1.0)) = 1.0
        _OcclusionBias ("Roof Occlusion Height Bias (m)", Float) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "IgnoreProjector" = "True"
        }

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

            #define WX_MAX_LIGHTS 8

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_CretaceaRainOcclusionTex);
            SAMPLER(sampler_CretaceaRainOcclusionTex);
            float4 _CretaceaRainOcclusionParams; // xy = centre XZ, z = half extent, w = valid (> 0)
            float  _RainIntensity;

            // Published by WeatherLightSampler (absolute world space).
            float4 _CretaceaWxLightPos[WX_MAX_LIGHTS];    // xyz position, w = 1 / range^2
            float4 _CretaceaWxLightColor[WX_MAX_LIGHTS];  // rgb = linear colour x candela x dimmer, w = cap (nits)
            float4 _CretaceaWxLightSpot[WX_MAX_LIGHTS];   // xyz = forward, w = angular scale
            float4 _CretaceaWxLightSpot2[WX_MAX_LIGHTS];  // x = angular offset, y = 1 for a lamp under a roof
            float  _CretaceaWxLightCount;
            float4 _CretaceaWxDirColor0;  // rgb lux (moon / sun)
            float4 _CretaceaWxDirFwd0;    // xyz direction the light travels
            float4 _CretaceaWxDirColor1;  // rgb lux (lightning)
            float4 _CretaceaWxDirFwd1;
            float4 _CretaceaWxAmbient;    // rgb lux (sky dome, isotropic)

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _NearFade;
                float _UseTexture;
                float _Mode;
                float _IsSplash;
                float _Scatter;
                float _Brightness;
                float _Softness;
                float _StreakAxis;
                float _Occlusion;
                float _EmissionNits;
                float _MaxNits;
                float _ExposedCap;
                float _UseRoofOcclusion;
                float _OcclusionBias;
                float _SkyLight;
                float _FlashLight;
                float _IndoorLights;
                float _CoverageDistance;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float4 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float4 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;   // absolute
                float3 positionRWS: TEXCOORD2;   // camera relative
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 posRWS = TransformObjectToWorld(input.positionOS);
                o.positionRWS = posRWS;
                o.positionWS  = GetAbsolutePositionWS(posRWS);
                o.positionCS  = TransformWorldToHClip(posRWS);
                o.color       = input.color;
                o.uv          = float4(input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw, input.uv.zw);
                return o;
            }

            // Two-lobe phase for water drops: strong forward scattering (back-lit rain glows around lamps),
            // a little back scattering (rain in the flashlight beam), normalised to about 1 on average.
            float WxPhase(float cosTheta)
            {
                const float gF = 0.62;
                const float gB = -0.35;
                float hgF = (1.0 - gF * gF) / pow(max(1e-3, 1.0 + gF * gF - 2.0 * gF * cosTheta), 1.5);
                float hgB = (1.0 - gB * gB) / pow(max(1e-3, 1.0 + gB * gB - 2.0 * gB * cosTheta), 1.5);
                return min(6.0, 0.45 + 0.40 * hgF + 0.15 * hgB);
            }

            // Radiance in nits; lampShare = the part of it from point lights, the flashlight and the lightning (light that
            // really reaches a drop and makes a whole streak glint) against the sky / moon share.
            float3 WxIncomingRadiance(float3 posWS, float3 toCam, float useLobes, out float lampShare)
            {
                float scatter = _Scatter * _Brightness;
                float sky = scatter * _SkyLight;
                float3 Lsky = _CretaceaWxAmbient.rgb * sky;

                float cos0 = dot(_CretaceaWxDirFwd0.xyz, toCam);
                Lsky += _CretaceaWxDirColor0.rgb * sky * lerp(1.0, WxPhase(cos0), useLobes);
                // the lightning has its own weight: a strike lights the whole sheet of rain (not cut by the sky weight)
                float cos1 = dot(_CretaceaWxDirFwd1.xyz, toCam);
                float3 L = _CretaceaWxDirColor1.rgb * (scatter * _FlashLight) * lerp(1.0, WxPhase(cos1), useLobes);

                int count = (int)_CretaceaWxLightCount;
                [loop] for (int i = 0; i < WX_MAX_LIGHTS; i++)
                {
                    if (i >= count) break;
                    float3 d = posWS - _CretaceaWxLightPos[i].xyz;
                    float dist2 = max(dot(d, d), 0.04);
                    float3 dirFromLight = d * rsqrt(dist2);
                    float win = saturate(1.0 - Sq(dist2 * _CretaceaWxLightPos[i].w));
                    win *= win;
                    float ang = saturate(dot(dirFromLight, _CretaceaWxLightSpot[i].xyz) * _CretaceaWxLightSpot[i].w + _CretaceaWxLightSpot2[i].x);
                    ang *= ang;
                    float lux = win * ang / dist2;
                    float phase = lerp(1.0, WxPhase(dot(dirFromLight, toCam)), useLobes);
                    float weight = lerp(1.0, _IndoorLights, _CretaceaWxLightSpot2[i].y);
                    float3 Li = _CretaceaWxLightColor[i].rgb * (lux * scatter * phase * weight);
                    // never brighter than the lamp that lights it: a soft shoulder, not a clamp, so drops in a beam's hot
                    // spot stay brighter than drops in its spill (a hard cap made both the same white)
                    float lum = Luminance(Li);
                    Li *= 1.0 / (1.0 + lum / max(_CretaceaWxLightColor[i].w, 1e-3));
                    L += Li;
                }
                float lumLamp = Luminance(L), lumSky = Luminance(Lsky);
                lampShare = saturate(lumLamp / (lumLamp + lumSky + 1e-4));
                L += Lsky;
                float lumAll = lumLamp + lumSky;
                // in daylight the sky lights the rain with hundreds of nits: the cap follows the scene white there, so
                // light rain at dusk shows as faint lines against the trees (at night _MaxNits is the larger value)
                float cap = max(_MaxNits, _ExposedCap / max(GetCurrentExposureMultiplier(), 1e-6));
                return L / (1.0 + lumAll / max(cap, 1e-3));
            }

            float4 Frag(Varyings i) : SV_Target
            {
                int mode = (int)round(max(_Mode, _IsSplash));

                // Roof occlusion: nothing of the rain exists under a roof (still visible outside the window).
                if (_UseRoofOcclusion > 0.5 && _CretaceaRainOcclusionParams.w > 0.5)
                {
                    float2 rel = (i.positionWS.xz - _CretaceaRainOcclusionParams.xy) / (_CretaceaRainOcclusionParams.z * 2.0) + 0.5;
                    if (all(rel > 0.0) && all(rel < 1.0))
                    {
                        float roofY = SAMPLE_TEXTURE2D_LOD(_CretaceaRainOcclusionTex, sampler_CretaceaRainOcclusionTex, rel, 0).r;
                        if (roofY > -5000.0 && i.positionWS.y < roofY - _OcclusionBias)
                            discard;
                    }
                }

                float2 uv = i.uv.xy;
                float age = saturate(i.uv.z);
                float rnd = i.uv.w;
                float2 c = uv * 2.0 - 1.0;
                if (_StreakAxis > 0.5) c = c.yx;

                float shape;
                if (mode == 0)       // streak: a motion-blurred drop = an even line with soft ends, thin soft core
                {
                    float across = exp(-Sq(c.x * 2.2) * _Softness);
                    float body = saturate((1.0 - abs(c.y)) / 0.55);
                    float along = body * body * (3.0 - 2.0 * body);
                    // each drop its own brightness (drop size and the angle it catches the light at)
                    shape = across * along * (0.45 + 0.85 * rnd * rnd);
                }
                else if (mode == 1)  // ripple ring expanding over its life + a short-lived impact dot
                {
                    float r = length(c);
                    float radius = lerp(0.08, 0.9, sqrt(age));
                    float width = lerp(0.16, 0.06, age);
                    float ring = exp(-Sq((r - radius) / width));
                    float ring2 = exp(-Sq((r - radius * 0.55) / (width * 0.8))) * 0.45;   // the second, inner wave
                    float dot0 = exp(-Sq(r / 0.12)) * saturate(1.0 - age * 4.0) * 0.4;      // impact point (a disturbance, not a coin)
                    shape = ((ring + ring2) * Sq(1.0 - age) + dot0) * saturate(1.0 - r);
                }
                else                 // mote / spark: soft dot
                {
                    shape = exp(-dot(c, c) * 3.0 * _Softness);
                    if (mode == 2) shape *= sin(PI * age);
                    else shape *= 1.0 - age * 0.6;
                }

                if (_UseTexture > 0.5)
                    shape *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;

                float3 toCamRWS = -i.positionRWS;
                float camDist = length(toCamRWS);
                float3 toCam = toCamRWS / max(camDist, 1e-4);
                float nearFade = smoothstep(_NearFade.x, _NearFade.y, camDist);

                float alpha = saturate(shape * _Color.a * i.color.a * nearFade);
                if (mode <= 1) alpha *= saturate(_RainIntensity * 1.15);
                // A drop is a few millimetres wide, far below a pixel beyond a couple of metres, but the renderer holds the
                // quad at >= 1-2 px: scale the opacity by the real pixel coverage instead, so the rain is dense and
                // visible close to the camera and thins into a faint sheet with distance (never bead chains).
                if (alpha < 0.002)
                    discard;

                float3 radiance;
                float occlusion = _Occlusion;
                if (mode == 3)
                {
                    radiance = _Color.rgb * i.color.rgb * _EmissionNits;
                    occlusion = 0.0;
                }
                else
                {
                    float lampShare;
                    radiance = WxIncomingRadiance(i.positionWS, toCam, mode == 1 ? 0.0 : 1.0, lampShare) * _Color.rgb * i.color.rgb;
                    // the coverage thinning applies to the dim sky / moon share only: a streak in a lamp cone, the beam or
                    // a strike glints as a whole line (that is how rain is seen at all at night)
                    if (mode == 0) alpha *= lerp(saturate(_CoverageDistance / max(camDist, 1e-3)), 1.0, lampShare);
                }
                if (alpha < 0.002)
                    discard;

                float3 rgb = radiance * GetCurrentExposureMultiplier() * alpha;

                // HDRP fog / volumetric fog in front of the particle.
                PositionInputs posInput = GetPositionInput(i.positionCS.xy, _ScreenSize.zw, i.positionCS.z, i.positionCS.w, i.positionRWS);
                float3 fogColor, fogOpacity;
                EvaluateAtmosphericScattering(posInput, toCam, fogColor, fogOpacity);
                rgb *= (1.0 - fogOpacity);

                return float4(rgb, alpha * occlusion);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
