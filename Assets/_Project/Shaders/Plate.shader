// The Sand table's vibrating plate, on a quad lying flat: dark glass with rounded corners and a glowing rim. The
// still lines of the current vibration mode (where the sand gathers) glow faintly on it, brighter on the beat.
// The Chladni figure for modes n, m on a square plate: cos(n x) cos(m y) - cos(m x) cos(n y), x and y in 0..pi.
Shader "MusicInYourHands/Plate"
{
    Properties
    {
        _Body ("Body", Color) = (0.06, 0.05, 0.09, 0.55)
        _Rim ("Rim and lines", Color) = (0.49, 0.95, 0.79, 1)
        _N ("Mode n (set by SandTable)", Float) = 2
        _M ("Mode m (set by SandTable)", Float) = 3
        _Kick ("Beat 0-1 (set by SandTable)", Float) = 0
        _Alpha ("Fade (set by SandTable)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-1" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _Body;
                float4 _Rim;
                float _N;
                float _M;
                float _Kick;
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
                // Rounded square: distance to the edge, negative inside.
                float2 q = abs(p) - 0.9;
                float box = length(max(q, 0)) + min(max(q.x, q.y), 0) - 0.06;
                float inside = smoothstep(0.01, -0.01, box);
                float rim = exp(-pow((box + 0.02) / 0.012, 2));

                float2 x = input.uv * 3.14159265;
                float f = cos(_N * x.x) * cos(_M * x.y) - cos(_M * x.x) * cos(_N * x.y);
                float lines = exp(-abs(f) * 22) * (0.18 + _Kick * 0.3) * inside;

                float3 color = _Body.rgb * (1 + 0.4 * (1 - length(p) * 0.6)) + _Rim.rgb * (lines + rim * (0.8 + _Kick * 0.6));
                float alpha = _Body.a * inside + lines * 0.6 + rim;
                return half4(color, saturate(alpha) * _Alpha);
            }
            ENDHLSL
        }
    }
}
