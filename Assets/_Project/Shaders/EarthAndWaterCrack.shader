// Earth and water: the cracked floor around a rising rock, on a quad lying flat on the ground. Wandering cracks run out
// from the rock's footprint (_Inner, a share of the quad's half width), with finer branches between them, and glow from
// below with _Glow: brightly while the rock moves or on the beat, faintly while it stands. Additive.
Shader "MusicInYourHands/EarthAndWaterCrack"
{
    Properties
    {
        _Color ("Glow", Color) = (1, 0.58, 0.22, 1)
        _Glow ("Glow 0-1 (set by EarthAndWater)", Float) = 0.3
        _Inner ("Footprint radius 0-1 (set by EarthAndWater)", Float) = 0.35
        _Seed ("Seed (set by EarthAndWater)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float _Glow;
                float _Inner;
                float _Seed;
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

            float Hash1(float x) { return frac(sin(x * 127.1 + 31.7) * 43758.5453); }

            float Noise1(float x)
            {
                float i = floor(x), f = frac(x);
                return lerp(Hash1(i), Hash1(i + 1), f * f * (3 - 2 * f));
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // One family of cracks: count of them around the circle, each wandering within its own slice of angle and
            // ending at its own length (between reach.x and reach.y). Width in uv units.
            float Cracks(float2 p, float r, float count, float offset, float2 reach, float width, float seed)
            {
                float a = atan2(p.y, p.x) / 6.2831853 + 0.5;
                float k = a * count + offset;
                float cell = floor(k);
                float wander = (Noise1(r * 6 + cell * 3.7 + seed) - 0.5) * 0.6;
                float d = abs(frac(k) - 0.5 + wander) * 6.2831853 * r / count;
                float end = lerp(reach.x, reach.y, Hash1(cell + seed));
                return smoothstep(width, width * 0.2, d) * smoothstep(end, end - 0.2, r) * step(0.25, Hash1(cell * 1.3 + seed + 5));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv * 2 - 1;
                float r = length(p);
                float wide = Cracks(p, r, 8, 0, float2(0.65, 1.0), 0.026, _Seed);
                float fine = Cracks(p, r, 19, 0.37, float2(_Inner + 0.12, 0.7), 0.014, _Seed + 9);
                float outside = smoothstep(_Inner - 0.03, _Inner + 0.04, r);
                float halo = exp(-pow((r - _Inner) / 0.1, 2)) * 0.4;
                float glow = (max(wide, fine * 0.7) * outside + halo) * _Glow * smoothstep(1.0, 0.85, r);
                return half4(_Color.rgb * (0.7 + glow * 0.3), saturate(glow));
            }
            ENDHLSL
        }
    }
}
