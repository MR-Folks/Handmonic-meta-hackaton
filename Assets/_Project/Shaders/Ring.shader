// A glowing ring on a quad, for the waves that leave the palms on each beat.
// _Radius (0-1 of the quad) grows over the wave's life; a faint glow fills the inside behind the ring.
Shader "MusicInYourHands/Ring"
{
    Properties
    {
        _Color ("Color", Color) = (0.62, 0.56, 1, 1)
        _Radius ("Radius 0-1", Float) = 0.5
        _Thickness ("Thickness 0-1", Float) = 0.02
        _Alpha ("Alpha", Float) = 1
        _InnerGlow ("Inner glow", Float) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Radius;
                float _Thickness;
                float _Alpha;
                float _InnerGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float r = length(input.uv * 2 - 1);
                float t = max(_Thickness, 1e-4);
                float ring = exp(-pow((r - _Radius) / t, 2));
                float halo = exp(-pow((r - _Radius) / (t * 3), 2)) * 0.22;
                float inside = saturate(1 - r / max(_Radius, 1e-3)) * _InnerGlow * step(r, _Radius);
                float a = saturate((ring + halo + inside) * _Alpha * _Color.a);
                float3 color = lerp(_Color.rgb, 1, ring * 0.35);
                return half4(color, a);
            }
            ENDHLSL
        }
    }
}
