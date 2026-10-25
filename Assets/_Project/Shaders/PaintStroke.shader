// A stroke of spray paint, drawn on LineStrip strips (uv.x along the stroke, uv.y across it): a solid core of
// colour that thins into a speckled overspray at its edges, the way a can sprays, with no glow. Vertex colour sets
// the paint and its fade.
Shader "MusicInYourHands/PaintStroke"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
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
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

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
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float across = abs(input.uv.y - 0.5) * 2;
                // Solid paint in the middle with a slightly wobbly edge.
                float grain = Hash(floor(input.positionWS * 1400));
                float core = smoothstep(0.62 + grain * 0.12, 0.45, across);
                // Overspray: fine speckles getting sparser towards the outside.
                float speckle = step(0.35 + across * 0.6, Hash(floor(input.positionWS * 900) + 7.0)) * smoothstep(1.0, 0.4, across);
                float alpha = max(core * 0.92, speckle * 0.7);
                float3 color = _Color.rgb * input.color.rgb * lerp(0.82, 1.05, core);
                return half4(color, saturate(alpha * input.color.a * _Color.a * _Intensity));
            }
            ENDHLSL
        }
    }
}
