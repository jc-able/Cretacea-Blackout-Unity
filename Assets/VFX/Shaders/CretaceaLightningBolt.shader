Shader "Cretacea/HDRP/LightningBolt"
{
    // Additive self-lit lightning channel / cloud glow, in nits, pre-exposed with HDRP's exposure and seen through
    // the fog (partially: a strike is bright enough to cut through rain haze). Intensity follows the global
    // _LightningFlashIntensity set by AtmosphereController, so the bolt flickers with the double stroke.
    //   mesh uv.x: -1..1 across the ribbon, uv.y: 0..1 along it; vertex colour alpha = branch strength
    Properties
    {
        _Color ("Colour", Color) = (0.80, 0.88, 1.0, 1)
        _CoreNits ("Core luminance (nits)", Float) = 9000
        _GlowNits ("Halo luminance (nits)", Float) = 180
        [Enum(Bolt,0,CloudGlow,1)] _Mode ("Mode", Float) = 0
        _FogPenetration ("Fog penetration (0 = fully fogged)", Range(0, 1)) = 0.55
        _Strength ("Strength (set per strike)", Float) = 1
        _UseLocalFlash ("Use own flash level (distant cloud flicker)", Float) = 0
        _LocalFlash ("Own flash level", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+50" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"

            float _LightningFlashIntensity;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _CoreNits;
                float _GlowNits;
                float _Mode;
                float _FogPenetration;
                float _Strength;
                float _UseLocalFlash;
                float _LocalFlash;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 positionRWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 rws = TransformObjectToWorld(v.positionOS);
                o.positionRWS = rws;
                o.positionCS = TransformWorldToHClip(rws);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float flash = saturate(_UseLocalFlash > 0.5 ? _LocalFlash : _LightningFlashIntensity) * _Strength;
                if (flash < 0.003) discard;

                float L;
                if (_Mode < 0.5)
                {
                    float x = i.uv.x;
                    float core = exp(-Sq(x * 9.0));
                    float halo = exp(-Sq(x * 2.2));
                    float tip = saturate(i.uv.y * 12.0) * saturate((1.0 - i.uv.y) * 4.0 + 0.25);
                    L = (core * _CoreNits + halo * _GlowNits) * i.color.a * tip;
                }
                else
                {
                    float2 c = i.uv * 2.0 - 1.0;
                    float r = length(c);
                    // lit cloud underside: soft, wide, brightest low in the patch
                    L = _GlowNits * exp(-r * r * 3.5) * saturate(1.0 - r) * (0.7 + 0.3 * saturate(-c.y + 0.3)) * i.color.a;
                }

                float3 rgb = _Color.rgb * L * flash * GetCurrentExposureMultiplier();

                float3 V = normalize(-i.positionRWS);
                PositionInputs posInput = GetPositionInput(i.positionCS.xy, _ScreenSize.zw, i.positionCS.z, i.positionCS.w, i.positionRWS);
                float3 fogColor, fogOpacity;
                EvaluateAtmosphericScattering(posInput, V, fogColor, fogOpacity);
                rgb *= pow(saturate(1.0 - fogOpacity), 1.0 - _FogPenetration);
                return float4(rgb, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
