// A soft round light: bright core, smooth falloff. Used for fingertip lights and spark particles.
// Draws additively, and also writes alpha so it shows over passthrough.
Shader "MusicInYourHands/Glow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _Softness ("Softness", Float) = 2.2
        _Core ("White-hot core", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest [_ZTest]
            Blend SrcAlpha One, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Softness;
                float _Core;
                float _ZTest;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float d = length(input.uv * 2 - 1);
                float core = saturate(1 - d);
                float glow = pow(core, _Softness);
                float3 color = _Color.rgb * input.color.rgb;
                color = lerp(color, 1, pow(core, 8) * _Core);
                float alpha = saturate(glow * _Color.a * input.color.a * _Intensity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
