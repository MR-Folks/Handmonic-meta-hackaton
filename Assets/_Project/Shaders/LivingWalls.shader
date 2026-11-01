// Living skin's room: a wall, floor or ceiling quad (its size in metres in _Size) that the palm waves land on. Each wave
// (_Waves: world position, age; _WaveColors: colour, strength) spreads as a ring, and veins of the same light stay lit
// behind it for a moment, so the room seems to have the same living skin as the hands. _Pulse wakes the veins a little
// on the bass, everywhere at once. A wave with a negative age has not arrived yet.
Shader "MusicInYourHands/LivingWalls"
{
    Properties
    {
        _Color ("Resting veins", Color) = (0.62, 0.56, 1, 1)
        _Size ("Quad size (m) (set by LivingWalls)", Vector) = (3, 2.5, 0, 0)
        _Speed ("How fast waves spread (m/s)", Float) = 1
        _Life ("How long a wave lasts (s)", Float) = 1.6
        _Pulse ("Bass 0-1 (set by LivingWalls)", Float) = 0
        _Alpha ("Fade", Float) = 1
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
                float4 _Size;
                float _Speed;
                float _Life;
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

            // Thin, wandering lines where two warped wave fields cross zero, 1 on a vein and 0 between them.
            float Veins(float2 m)
            {
                float2 w = m + 0.16 * sin(m.yx * 5.3 + float2(1.7, 4.1));
                float a = sin(w.x * 6.1 + sin(w.y * 3.7) * 1.6);
                float b = sin(w.y * 5.3 + sin(w.x * 4.3) * 1.9);
                return smoothstep(0.14, 0.02, min(abs(a), abs(b)));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float veins = Veins(input.uv * _Size.xy);

                float3 color = _Color.rgb * veins * _Pulse * 0.25;
                float alpha = veins * _Pulse * 0.1;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float age = _Waves[i].w;
                    if (age < 0 || age > _Life) continue;
                    float d = distance(input.positionWS, _Waves[i].xyz);
                    float reach = age * _Speed;
                    float ring = exp(-pow((d - reach) / 0.05, 2));
                    // Behind the ring the veins stay lit, dimming back towards where the wave landed.
                    float behind = step(d, reach) * exp(-(reach - d) / 0.45);
                    float fade = (1 - age / _Life) * (1 - age / _Life) * _WaveColors[i].a;
                    float k = (ring * (0.35 + veins * 0.65) + behind * veins * 0.9) * fade;
                    color += _WaveColors[i].rgb * k;
                    alpha += k * 0.7;
                }

                // Fade out towards the quad's edges and corners, so the room has no hard frame.
                float2 e = min(input.uv, 1 - input.uv);
                float edge = smoothstep(0, 0.06, min(e.x, e.y));
                return half4(color, saturate(alpha * edge * _Alpha));
            }
            ENDHLSL
        }
    }
}
