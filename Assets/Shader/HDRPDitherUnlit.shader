Shader "Custom/HDRPDitherUnlit"
{
    Properties
    {
        _MainTex("Base Texture", 2D) = "white" {}
        _BaseColor("Color Tint", Color) = (1,1,1,1)
        _DitherAmount("Dither Amount", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_Position;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _BaseColor;
            float _DitherAmount;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 finalColor = texColor * _BaseColor;

                float ditherThreshold = InterleavedGradientNoise(input.positionCS.xy, 0);
                float keep = saturate(1.0 - _DitherAmount);
                clip(keep - ditherThreshold);

                return float4(finalColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
