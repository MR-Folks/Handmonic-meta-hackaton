// Sound pool's water light on the room: a wall, floor or ceiling quad from RoomWaves. It is the same shifting web of light
// the pool throws on the floor beneath it (Caustics shader), carried on across the whole room: the same pattern laid out
// in world space at the same scale, as bright as under the pool there (_Origin: where that is, and its strength) and
// fading away with distance, so it climbs a little way up the walls and is gone before the far side of the room. Each
// wave the pool sends (_Waves: world position, age; _WaveColors: colour, strength) travels out from it as a brighter
// swell of that same web, fading just the same.
Shader "MusicInYourHands/RoomRipples"
{
    Properties
    {
        _Color ("Water light", Color) = (0.45, 0.85, 1, 1)
        _Scale ("Pattern scale (per metre), as the Caustics material's", Float) = 7
        _Reach ("How far (m) from under the pool the light reaches before it has faded away", Float) = 3
        _Speed ("How fast waves spread (m/s)", Float) = 1.3
        _Life ("How long a wave lasts (s)", Float) = 3
        _Origin ("Where the light comes from, and its strength there (set by SoundPool)", Vector) = (0, 0, 0, 0.5)
        _Size ("Quad size (m) (set by RoomWaves)", Vector) = (3, 2.5, 0, 0)
        _Pulse ("Bass 0-1 (set by RoomWaves)", Float) = 0
        _Alpha ("Fade (set by RoomWaves)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float _Scale;
                float _Reach;
                float _Speed;
                float _Life;
                float4 _Origin;
                float4 _Size;
                float _Pulse;
                float _Alpha;
                float4 _Waves[8];
                float4 _WaveColors[8];
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
                return output;
            }

            // The same web as the light under the pool (Caustics shader): two warped interference grids, with light
            // gathering where either crosses zero.
            float Caustic(float2 p, float t)
            {
                float2 a = p + float2(sin(p.y * 1.7 + t * 0.9), cos(p.x * 1.3 - t * 0.7)) * 0.65;
                float2 b = p * 1.37 + float2(cos(p.y * 1.1 - t * 0.6), sin(p.x * 1.9 + t * 0.8)) * 0.65;
                float l1 = abs(sin(a.x * 2.0) * sin(a.y * 2.0));
                float l2 = abs(sin(b.x * 2.0 + 1.3) * sin(b.y * 2.0 + 0.7));
                return pow(1.0 - l1, 12.0) * 0.7 + pow(1.0 - l2, 12.0) * 0.55;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p = input.positionWS;
                // Floor and ceiling take the pattern across x and z, as under the pool; walls across their own face.
                float3 n = normalize(cross(ddy(p), ddx(p)));
                float2 plane = abs(n.y) > 0.5 ? p.xz : abs(n.x) > abs(n.z) ? p.zy : p.xy;
                float web = saturate(Caustic(plane * _Scale, _Time.y * 0.8));

                // As bright as under the pool there, fading smoothly to nothing at _Reach.
                float d = distance(p, _Origin.xyz);
                float falloff = saturate(1 - d / _Reach);
                falloff *= falloff;
                float strength = _Origin.w;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float age = _Waves[i].w;
                    if (age < 0 || age > _Life) continue;
                    float reach = age * _Speed;
                    float swell = exp(-pow((distance(p, _Waves[i].xyz) - reach) / 0.3, 2));
                    float fade = (1 - age / _Life) * (1 - age / _Life) * _WaveColors[i].a;
                    strength += swell * fade * 0.9;
                }

                // The same colour and brightness as the Caustics patch, so the two meet without a seam.
                float3 color = lerp(_Color.rgb, 1, web * 0.35);
                float alpha = web * strength * falloff * 0.7;
                // Fade out towards the quad's edges and corners, so the room has no hard frame.
                float2 e = min(input.uv, 1 - input.uv);
                float edge = smoothstep(0, 0.06, min(e.x, e.y));
                return half4(color, saturate(alpha * edge * _Alpha));
            }
            ENDHLSL
        }
    }
}
