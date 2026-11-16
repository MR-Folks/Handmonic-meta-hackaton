// The snap preview: a glowing outline of a cassette where it would sit in the radio, brightest round its edges,
// with soft lines drifting up through it and a gentle pulse. Additive, and it writes alpha so it shows over
// passthrough. _Alpha fades it in and out.
Shader "MusicInYourHands/CassetteDeckGhost"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 1, 0.8, 1)
        _Alpha ("Fade", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+15" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
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
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float edge = pow(1 - saturate(abs(dot(n, v))), 2);
                float t = _Time.y;
                float lines = 0.65 + 0.35 * sin(input.positionWS.y * 520 - t * 7);
                float pulse = 0.8 + 0.2 * sin(t * 6.5);
                float a = saturate((0.22 + edge * 1.3) * lines * pulse * _Alpha * _Color.a);
                return half4(lerp(_Color.rgb, 1, edge * 0.4), a);
            }
            ENDHLSL
        }
    }
}
