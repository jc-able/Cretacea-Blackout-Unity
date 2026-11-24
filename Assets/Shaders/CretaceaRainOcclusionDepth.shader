Shader "Cretacea/Internal/RainOcclusionHeight"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float worldY      : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 posRWS = TransformObjectToWorld(input.positionOS);
                float3 posWS  = GetAbsolutePositionWS(posRWS);
                o.positionCS  = TransformWorldToHClip(posRWS);
                o.worldY      = posWS.y;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                return float4(i.worldY, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
