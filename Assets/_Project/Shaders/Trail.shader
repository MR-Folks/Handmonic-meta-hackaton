// A Light trails ribbon: a flat band of light along the path a fingertip drew. Each vertex sits on the band's
// centre line with the half-width vector in its normal and the side (0 or 1) in uv.y, so the whole sculpture can
// swell on the beat here in the shader without rebuilding meshes. Vertex colour gives the hue and fade.
Shader "MusicInYourHands/Trail"
{
    Properties
    {
        _Kick ("Beat 0-1 (set by LightTrails)", Float) = 0
        _Swell ("How much the ribbons swell on the beat", Float) = 0.45
        _Clock ("Time (set by LightTrails)", Float) = 0
        _Alpha ("Fade (set by LightTrails)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+7" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float _Kick;
                float _Swell;
                float _Clock;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
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
                float side = input.uv.y * 2 - 1;
                float3 p = input.positionOS.xyz + input.normalOS * side * (1 + _Kick * _Swell);
                output.positionCS = TransformObjectToHClip(p);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float across = abs(input.uv.y * 2 - 1);
                float body = pow(1 - across, 1.5);
                float core = pow(1 - across, 8);
                // A shimmer that runs along the ribbon.
                float shimmer = pow(0.5 + 0.5 * sin(input.uv.x * 40 - _Clock * 4), 8) * 0.35;
                float3 color = lerp(input.color.rgb, 1, core * 0.6 + shimmer * 0.5);
                float alpha = (body * 0.5 + core * 0.6 + shimmer * body) * (1 + _Kick * 0.5);
                return half4(color, saturate(alpha * input.color.a * _Alpha));
            }
            ENDHLSL
        }
    }
}
