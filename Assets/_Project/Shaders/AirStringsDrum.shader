// The taiko drums of the Air strings world: a dark barrel lit at its edges, glowing hoops and rims, and a skin that
// ripples out from where it was struck. One mesh per drum; uv1.x says which part a vertex belongs to (0 shell, 1 skin,
// 2 rim, 3 stand). Per drum, through its property block: _Color, _Glow (a strike), _Pulse (the music), _Wake and _Hit.
// Opaque, so the drums read as solid things standing in the room.
Shader "MusicInYourHands/AirStringsDrum"
{
    Properties
    {
        _Color ("Neon colour", Color) = (1, 0.36, 0.78, 1)
        _Body ("Body colour", Color) = (0.09, 0.035, 0.14, 1)
        _Skin ("Skin colour", Color) = (0.62, 0.5, 0.6, 1)
        _Glow ("Strike glow 0-1", Float) = 0
        _Pulse ("Music pulse 0-1", Float) = 0
        _Wake ("Awake 0-1", Float) = 1
        _Hit ("Strike: xy on the skin (-1..1), z seconds since, w strength", Vector) = (0, 0, 9, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Body;
                float4 _Skin;
                float _Glow;
                float _Pulse;
                float _Wake;
                float4 _Hit;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 part : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float part : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.part = input.part.x;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float edge = pow(saturate(1 - abs(dot(n, v))), 3);
                // A soft light from above, so the barrels have shape without any real lights.
                float light = 0.55 + 0.45 * saturate(n.y * 0.5 + 0.5);
                float3 neon = _Color.rgb;
                float3 color;

                if (input.part < 0.5)
                {
                    // Shell: uv.x runs round the barrel, uv.y from the skin (0) to the bottom (1).
                    float stave = 1 - smoothstep(0.0, 0.06, abs(frac(input.uv.x * 14 + 0.5) - 0.5));
                    float hoops = exp(-pow((input.uv.y - 0.1) / 0.035, 2)) + exp(-pow((input.uv.y - 0.9) / 0.035, 2));
                    color = _Body.rgb * light
                        + neon * (edge * (0.6 + _Pulse * 0.9 + _Glow) + stave * 0.1 + hoops * (0.55 + _Pulse * 0.8 + _Glow * 1.2));
                }
                else if (input.part < 1.5)
                {
                    // Skin: uv is the point on the skin, -1..1 across it. Faint rings, a glowing edge, and the strike:
                    // a bright spot where it landed and a ripple running out from it.
                    float r = length(input.uv);
                    float rim = smoothstep(0.84, 1.0, r);
                    float rings = (sin(r * 18.85) * 0.5 + 0.5) * 0.02 + exp(-pow((r - 0.62) / 0.05, 2)) * 0.12;
                    float age = _Hit.z;
                    float d = distance(input.uv, _Hit.xy);
                    float ripple = exp(-pow((d - age * 2.6) / 0.1, 2)) * exp(-age * 2.8) * _Hit.w;
                    float spot = exp(-d * d / 0.06) * exp(-age * 6) * _Hit.w;
                    color = _Skin.rgb * light * (0.8 + _Pulse * 0.4)
                        + neon * (rim * (0.7 + _Pulse + _Glow) + rings + ripple * 1.7 + spot * 1.3 + _Glow * 0.2);
                }
                else if (input.part < 2.5)
                {
                    // Rims: bright neon, brighter on the beat and when struck.
                    color = neon * (1.05 + _Pulse * 0.9 + _Glow * 1.4) + 0.08;
                }
                else
                {
                    // Stand.
                    color = _Body.rgb * 0.7 + neon * (0.18 + edge * 0.3);
                }

                color *= lerp(0.15, 1, saturate(_Wake));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
