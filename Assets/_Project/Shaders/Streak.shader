// A falling drop of light: a bright round head with a tail that narrows and fades behind it, like a teardrop with
// motion blur. Drawn on a quad whose bottom edge (uv.y = 0) is the head and top edge the end of the tail.
Shader "MusicInYourHands/Streak"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.72, 1, 1)
        _Intensity ("Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+12" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float _Intensity;
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
                float2 uv = input.uv;
                float taper = lerp(1, 0.12, saturate(uv.y));
                float across = abs(uv.x * 2 - 1) / taper;
                // Only the tail: it fades in just above the bottom edge, where the drop's round glow sits, so the
                // quad's edge never shows.
                float start = smoothstep(0.0, 0.14, uv.y);
                float body = exp(-across * across * 3) * pow(1 - uv.y, 1.8) * start;
                float core = exp(-across * across * 30) * pow(1 - uv.y, 3) * start;
                float3 color = lerp(_Color.rgb, 1, saturate(core * 0.6));
                float alpha = saturate((body * 0.75 + core) * _Intensity * _Color.a);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
