// Earth and water: a rock risen out of the floor. Solid, faceted stone in warm greys with grain and faint strata, lit
// from above and from the viewer's side so its faces always read; glowing cracks run through it (_Glow, brighter on
// the beat and while it moves) and a glowing seam marks where it comes out of the ground. Everything below _Ground
// (world height: the floor, or the furniture it rises from) is cut away, so the rock seems to push up through it.
Shader "MusicInYourHands/EarthAndWaterRock"
{
    Properties
    {
        _Stone ("Stone", Color) = (0.6, 0.53, 0.45, 1)
        _Dark ("Crevices", Color) = (0.25, 0.21, 0.18, 1)
        _Crack ("Glow in the cracks", Color) = (1, 0.55, 0.2, 1)
        _Tint ("Tint (set by EarthAndWater)", Color) = (1, 1, 1, 1)
        _Glow ("Crack glow 0-1 (set by EarthAndWater)", Float) = 0.2
        _Ground ("Ground height, world y (set by EarthAndWater)", Float) = 0
        _Seed ("Seed (set by EarthAndWater)", Float) = 0
        _Clock ("Time (set by EarthAndWater)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Stone;
                float4 _Dark;
                float4 _Crack;
                float4 _Tint;
                float _Glow;
                float _Ground;
                float _Seed;
                float _Clock;
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
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float above = input.positionWS.y - _Ground;
                clip(above);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float ndv = saturate(dot(n, v));

                // Grain, flecks and faint strata in the stone.
                float3 o = input.positionOS * 7 + _Seed * 3.1;
                float grain = Noise3(o) * 0.55 + Noise3(o * 2.7) * 0.3 + Noise3(o * 7.9) * 0.15;
                float strata = sin(input.positionOS.y * 31 + Noise3(o * 0.6) * 4) * 0.5 + 0.5;
                float3 albedo = lerp(_Dark.rgb, _Stone.rgb * _Tint.rgb, saturate(grain * 1.15 - 0.05 + strata * 0.18));
                float fleck = step(0.86, Noise3(o * 14));
                albedo = lerp(albedo, albedo * 1.35 + 0.04, fleck * 0.5);

                // Lit from above and from the viewer's side, with a cool rim against the room.
                float top = saturate(dot(n, normalize(float3(0.25, 1, 0.15))));
                float front = saturate(dot(n, v));
                float light = 0.32 + top * 0.5 + front * 0.45;
                float3 color = albedo * light + pow(1 - ndv, 3) * float3(0.12, 0.14, 0.16);

                // Cracks: thin, wandering lines through the rock that glow from inside.
                float c = abs(Noise3(input.positionOS * 3.3 + _Seed * 1.7) - 0.5) + abs(Noise3(input.positionOS * 7.1 - _Seed) - 0.5) * 0.35;
                float crack = 1 - smoothstep(0.0, 0.045, c);
                float pulse = 0.75 + 0.25 * sin(_Clock * 2.6 + input.positionOS.y * 9 + _Seed);
                color = lerp(color, _Dark.rgb * 0.5, crack * 0.65);
                color += _Crack.rgb * crack * _Glow * pulse * 1.5;

                // Darker where it comes out of the ground, with a glowing seam right at the ground line.
                color *= lerp(0.42, 1, saturate(above / 0.07));
                color += _Crack.rgb * exp(-above / 0.01) * saturate(_Glow * 1.4);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
