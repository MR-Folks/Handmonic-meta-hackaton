// The Waveform ribbon's silk: a see-through band of light. Colour runs from one hand's colour to the other's,
// soft sheen bands flow along it with the music, and its edges fade out so it reads as fabric, not a plank.
// The mesh's uv.x runs along the band (0-1) and uv.y across it; vertex alpha fades the ends and old trails.
Shader "MusicInYourHands/Silk"
{
    Properties
    {
        _ColorA ("Colour at the left hand", Color) = (1, 0.83, 0.42, 1)
        _ColorB ("Colour at the right hand", Color) = (1, 0.37, 0.68, 1)
        _Kick ("Beat 0-1 (set by WaveformRibbon)", Float) = 0
        _Clock ("Time (set by WaveformRibbon)", Float) = 0
        _Alpha ("Fade (set by WaveformRibbon)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+6" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _ColorA;
                float4 _ColorB;
                float _Kick;
                float _Clock;
                float _Alpha;
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
                float u = input.uv.x, v = input.uv.y;
                float edge = pow(sin(v * PI), 0.8);
                float rim = exp(-pow((abs(v - 0.5) - 0.46) / 0.035, 2));
                float sheen = pow(0.5 + 0.5 * sin(u * 26 - _Clock * 3.2 + v * 5), 6);
                float shimmer = pow(0.5 + 0.5 * sin(u * 61 + _Clock * 5.5 - v * 9), 10) * 0.5;
                float3 color = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0, 1, u));
                color = lerp(color, 1, (sheen + shimmer) * 0.45 + rim * 0.3);
                float alpha = (0.14 + sheen * 0.32 + shimmer * 0.2 + rim * 0.45 + _Kick * 0.12) * edge;
                return half4(color, saturate(alpha * input.color.a * _Alpha));
            }
            ENDHLSL
        }
    }
}
