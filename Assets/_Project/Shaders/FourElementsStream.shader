// Four elements: a stream of water flowing through the air, drawn on a camera-facing strip built by LineStrip (uv.x
// from the hand to the tail, uv.y across). It is shaded as a round, see-through tube: deep blue in its body, lighter at
// its edges, a bright highlight running along one side, and glints that flow down its length (_Length is the stream's
// length in metres, so they keep their spacing). Vertex colour tints it and fades its ends.
Shader "MusicInYourHands/FourElementsStream"
{
    Properties
    {
        _Deep ("Body colour", Color) = (0.1, 0.42, 0.85, 0.45)
        _Light ("Edge and glint colour", Color) = (0.55, 0.93, 1, 1)
        _Length ("Stream length in metres (set by FourElements)", Float) = 0.5
        _Clock ("Time (set by FourElements)", Float) = 0
        _Alpha ("Fade (set by FourElements)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+4" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _Deep;
                float4 _Light;
                float _Length;
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
                float across = input.uv.y * 2 - 1;
                float metres = input.uv.x * _Length;
                // A round tube seen from the side: thicker water in the middle, thin at the edges.
                float z = sqrt(saturate(1 - across * across));
                float edge = pow(1 - z, 1.6);
                // Glints flowing down the stream, wobbling across it.
                float flow = metres * 34 - _Clock * 7;
                float glint = pow(saturate(sin(flow + sin(across * 2.6 + metres * 11 - _Clock * 3) * 1.4) * 0.5 + 0.5), 14);
                float ripple = sin(metres * 61 - _Clock * 11 + across * 2) * 0.5 + 0.5;
                // The light catches one side of the tube in a bright streak.
                float streak = exp(-pow((across + 0.42 + (ripple - 0.5) * 0.12) / 0.13, 2)) * (0.65 + 0.35 * ripple);
                float3 color = lerp(_Deep.rgb, _Light.rgb, saturate(edge * 1.1 + glint * 0.7 + ripple * 0.12));
                color += (streak * 0.75 + glint * 0.45) * float3(0.9, 0.97, 1.0);
                float soft = smoothstep(1.0, 0.72, abs(across));
                float alpha = (_Deep.a * (0.5 + 0.5 * z) + edge * 0.45 + streak * 0.55 + glint * 0.4) * soft;
                return half4(color * input.color.rgb, saturate(alpha * input.color.a) * _Alpha);
            }
            ENDHLSL
        }
    }
}
