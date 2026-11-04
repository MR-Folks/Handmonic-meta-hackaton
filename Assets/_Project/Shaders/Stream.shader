// Sound pool's poured water: a camera-facing strip built by LineStrip along the falling stream (uv.x from the fingers,
// 0, to the water, 1; uv.y across). It reads as clear water: a faint body between two glassy edges, with glints racing
// down it, and near the bottom it breaks up into beads. _Length is the stream's length in metres, so the glints keep
// their size and speed however far it falls. Vertex colour fades its ends.
Shader "MusicInYourHands/Stream"
{
    Properties
    {
        _Color ("Water", Color) = (0.6, 0.9, 1, 1)
        _Length ("Stream length in metres (set by SoundPool)", Float) = 0.3
        _Clock ("Time (set by SoundPool)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+8" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float _Length;
                float _Clock;
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

            float Wave(float x) { return 0.5 + 0.5 * sin(x); }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float across = input.uv.y * 2 - 1;
                float x = input.uv.x * _Length;
                float body = exp(-across * across * 2.5);
                float edges = exp(-pow((abs(across) - 0.7) / 0.15, 2));
                // Glints running down with the water.
                float glint = pow(Wave(x * 55 - _Clock * 16 + across * 1.5), 5) * body;
                // Near the water the stream breaks into beads.
                float breakup = lerp(1, smoothstep(0.35, 0.75, Wave(x * 90 - _Clock * 22)), smoothstep(0.7, 1, input.uv.x));

                float3 color = lerp(_Color.rgb, 1, edges * 0.6 + glint * 0.5);
                float alpha = (body * 0.28 + edges * 0.45 + glint * 0.35) * breakup;
                return half4(color, saturate(alpha * input.color.a * _Color.a));
            }
            ENDHLSL
        }
    }
}
