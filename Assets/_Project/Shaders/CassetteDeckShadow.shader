// A soft contact shadow on a flat quad: a rounded rectangle (or, with equal sides and a full radius, a disc) that
// darkens what is under it, the real floor seen through passthrough included, so the deck sits in the room.
// _Shape: half width and half height of the dark core (0-1 of the quad), corner radius, softness.
Shader "MusicInYourHands/CassetteDeckShadow"
{
    Properties
    {
        _Color ("Shadow colour and strength", Color) = (0, 0, 0, 0.45)
        _Shape ("Core half size x, y, radius, softness (0-1)", Vector) = (0.7, 0.7, 0.2, 0.3)
        _Alpha ("Fade", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-60" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Shape;
                float _Alpha;
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
                float2 p = input.uv * 2 - 1;
                float2 d = abs(p) - (_Shape.xy - _Shape.z);
                float dist = length(max(d, 0)) + min(max(d.x, d.y), 0) - _Shape.z;
                float shade = 1 - smoothstep(-_Shape.w * 0.35, _Shape.w, dist);
                return half4(_Color.rgb, shade * shade * _Color.a * _Alpha);
            }
            ENDHLSL
        }
    }
}
