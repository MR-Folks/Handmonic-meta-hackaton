// A night sky seen through the ceiling: a flat quad whose colour comes from the direction you look along, not from where
// on the quad you look, so the sky sits at infinity beyond the room and doesn't move as you walk around. _MainTex holds
// the Milky Way and faint nebula; the sharp stars are drawn here, so they stay pin points at any resolution, and they
// twinkle with _Pulse. _Size is the quad's size in metres and _Soft how far in from its edges it fades to the room.
Shader "MusicInYourHands/Sky"
{
    Properties
    {
        _MainTex ("Milky Way", 2D) = "black" {}
        _Zenith ("Colour overhead", Color) = (0.012, 0.018, 0.06, 1)
        _Horizon ("Colour towards the walls", Color) = (0.05, 0.05, 0.16, 1)
        _Size ("Size (m)", Vector) = (10, 10, 0, 0)
        _Soft ("Edge fade (m)", Float) = 3
        _Pulse ("Music 0-1", Float) = 0
        _Alpha ("Fade", Float) = 1
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
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Zenith;
                float4 _Horizon;
                float4 _Size;
                float _Soft;
                float _Pulse;
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
                float3 look : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.look = positionWS - GetCameraPositionWS();
                return output;
            }

            float2 Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.xx + q.yz) * q.zy);
            }

            // One layer of stars: at most one in each cell of a grid over the sky.
            float Stars(float2 p, float cells, float share, float size, float pulse)
            {
                float2 grid = p * cells;
                float2 cell = floor(grid);
                float2 random = Hash(cell);
                if (random.x > share) return 0;
                float2 place = 0.25 + Hash(cell + 17) * 0.5;
                float2 offset = grid - cell - place;
                float spot = exp(-dot(offset, offset) / (size * size));
                float twinkle = 0.75 + 0.25 * sin(_Time.y * (1 + random.y * 3) + random.y * 40) + pulse * random.y * 0.5;
                return spot * (0.15 + 0.85 * random.y * random.y * random.y) * twinkle;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 look = normalize(input.look);
                // A stereographic projection, which keeps stars round all the way down to the walls. The sky turns very
                // slowly, as the real one does.
                float turn = _Time.y * 0.004;
                float2 flat = look.xz / (1 + max(look.y, 0));
                float2 sky = float2(flat.x * cos(turn) - flat.y * sin(turn), flat.x * sin(turn) + flat.y * cos(turn));

                float3 color = lerp(_Horizon.rgb, _Zenith.rgb, smoothstep(0.1, 0.8, look.y));
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sky * 0.62 + 0.5).rgb * (0.55 + _Pulse * 0.15);
                // A few bright stars, many faint ones.
                float2 tint = Hash(floor(sky * 18));
                float stars = Stars(sky, 18, 0.45, 0.035, _Pulse) * 1.5 + Stars(sky + 9.7, 46, 0.55, 0.04, _Pulse) * 0.6;
                color += stars * lerp(float3(1, 0.9, 0.78), float3(0.78, 0.86, 1), tint.y);

                float2 edge = (0.5 - abs(input.uv - 0.5)) * _Size.xy;
                float alpha = smoothstep(0, 1, saturate(min(edge.x, edge.y) / max(_Soft, 1e-3)));
                // Low on the horizon the flat sky stretches; let the room show through instead.
                alpha *= smoothstep(0.1, 0.3, look.y) * 0.97 * _Alpha;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
