// A soap-bubble button for the world menu, drawn on a camera-facing quad: a clear glass body, an iridescent rim,
// a glossy highlight and a small animated emblem of the world inside. _Icon picks the emblem (-1 for none, as the
// Bubble notes world uses it for its plain bubbles).
Shader "MusicInYourHands/Bubble"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.37, 0.68, 1)
        _Icon ("Emblem (-1 none, 0-11)", Float) = 0
        _Hover ("Hover 0-1", Float) = 0
        _Selected ("Current world 0-1", Float) = 0
        _Alpha ("Alpha", Float) = 1
        _Eq ("Music bands for the now-playing emblem", Vector) = (0.5, 0.5, 0.5, 0.5)
        _Paused ("Paused 0-1", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+30" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float _Icon;
                float _Hover;
                float _Selected;
                float _Alpha;
                float4 _Eq;
                float _Paused;
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

            float3 Hue(float h) { return saturate(abs(frac(h + float3(0, 2, 1) / 3) * 6 - 3) - 1); }

            float Line(float d, float w) { return smoothstep(w, 0, d); }

            // A tiny animated picture of each world, in the bubble's disc (-1..1).
            float Emblem(float2 p, float icon, float t)
            {
                float e = 0;
                if (icon < -0.5) return 0;
                if (icon < 0.5)
                {
                    // Living skin: waves leaving a palm.
                    float d = length(p);
                    e = pow(abs(sin(d * 9 - t * 3)), 12) * saturate(1 - d * 1.25) + exp(-d * d * 30) * 0.8;
                }
                else if (icon < 1.5)
                {
                    // Heartbeat orb: two petal shapes turning against each other.
                    float a = atan2(p.y, p.x), d = length(p);
                    float r1 = 0.5 * (0.8 + 0.2 * cos(5 * a + t * 1.3));
                    float r2 = 0.3 * (0.75 + 0.25 * cos(4 * a - t * 1.7));
                    e = Line(abs(d - r1), 0.05) + smoothstep(r2, r2 - 0.08, d) * 0.7;
                }
                else if (icon < 2.5)
                {
                    // Waveform ribbon: three waves between two points.
                    [unroll] for (int k = 0; k < 3; k++)
                    {
                        float y = 0.25 * sin(p.x * 5 + t * 2.4 + k * 1.9) * (1 - p.x * p.x);
                        e += Line(abs(p.y - y), 0.045) * step(abs(p.x), 0.8);
                    }
                }
                else if (icon < 3.5)
                {
                    // Air strings: four strings, one ringing.
                    [unroll] for (int k = 0; k < 4; k++)
                    {
                        float y = -0.36 + k * 0.24 + (k == 1 ? 0.06 * sin(p.x * 9 + t * 14) * (1 - p.x * p.x) : 0);
                        e += Line(abs(p.y - y), 0.035) * step(abs(p.x), 0.72);
                    }
                }
                else if (icon < 4.5)
                {
                    // Note rain: drops with tails.
                    [unroll] for (int k = 0; k < 5; k++)
                    {
                        float x = -0.44 + k * 0.22;
                        float y = 0.6 - frac(t * 0.45 + k * 0.37) * 1.2;
                        float2 q = p - float2(x, y);
                        e += exp(-dot(q, q) * 350) + Line(abs(q.x), 0.025) * saturate(1 - q.y / 0.3) * step(0, q.y) * 0.6;
                    }
                }
                else if (icon < 5.5)
                {
                    // Firefly choir: twinkling lights drifting about.
                    [unroll] for (int k = 0; k < 7; k++)
                    {
                        float2 q = p - 0.48 * float2(sin(k * 2.1 + t * 0.6), cos(k * 1.7 + t * 0.5));
                        e += exp(-dot(q, q) * 320) * (0.5 + 0.5 * sin(t * 3 + k * 2));
                    }
                }
                else if (icon < 6.5)
                {
                    // Now playing: an equaliser, or a pause sign while paused.
                    if (_Paused > 0.5)
                        return step(abs(abs(p.x) - 0.17), 0.08) * step(abs(p.y), 0.35);
                    float bars[4] = { _Eq.x, _Eq.y, _Eq.z, _Eq.w };
                    [unroll] for (int k = 0; k < 4; k++)
                    {
                        float x = -0.36 + k * 0.24;
                        float h = 0.15 + bars[k] * 0.55;
                        e += step(abs(p.x - x), 0.075) * step(-0.38, p.y) * step(p.y, -0.38 + h);
                    }
                }
                else if (icon < 7.5)
                {
                    // Listen: a microphone on a stand, with sound rings around it while listening.
                    float2 q = p - float2(0, 0.16);
                    e = smoothstep(0.02, 0, length(float2(q.x, max(abs(q.y) - 0.1, 0))) - 0.12);
                    float2 c = p - float2(0, 0.1);
                    e += Line(abs(length(c) - 0.24), 0.035) * step(c.y, 0);
                    e += step(abs(p.x), 0.03) * step(-0.33, p.y) * step(p.y, -0.14);
                    e += step(abs(p.x), 0.14) * step(abs(p.y + 0.35), 0.03);
                    float d = length(q);
                    e += _Selected * pow(abs(sin(d * 10 - t * 4)), 10) * smoothstep(0.3, 0.38, d) * saturate(1.15 - d * 1.6) * (0.4 + _Eq.x);
                }
                else if (icon < 8.5)
                {
                    // Sound pool: a liquid surface seen at an angle, rings spreading from a dipped fingertip.
                    float2 q = float2(p.x, (p.y + 0.1) * 2.4);
                    float d = length(q);
                    e = pow(abs(sin(d * 7 - t * 3)), 10) * saturate(1 - d * 0.9) * step(abs(p.y + 0.1), 0.4);
                    e += step(abs(p.x), 0.03) * step(-0.08, p.y) * step(p.y, 0.55) * 0.9;
                }
                else if (icon < 9.5)
                {
                    // Sound wind: streams of particles flowing around a small obstacle.
                    [unroll] for (int k = 0; k < 5; k++)
                    {
                        float lane = -0.48 + k * 0.24;
                        float bend = 0.16 * exp(-p.x * p.x * 9) * sign(lane) * step(0.01, abs(lane));
                        float y = lane + bend;
                        float dash = pow(saturate(sin(p.x * 14 - t * 6 + k * 1.7) * 0.5 + 0.5), 3);
                        e += Line(abs(p.y - y), 0.03) * dash * step(abs(p.x), 0.8);
                    }
                    e += smoothstep(0.1, 0.06, length(p)) * 0.8;
                }
                else if (icon < 10.5)
                {
                    // Bubble notes: three small bubbles rising.
                    [unroll] for (int k = 0; k < 3; k++)
                    {
                        float r = 0.14 + k * 0.05;
                        float2 c = float2(-0.36 + k * 0.36, 0.7 - frac(t * 0.25 + k * 0.33) * 1.4);
                        e += Line(abs(length(p - c) - r), 0.035) * 0.9;
                    }
                }
                else
                {
                    // Sand table: a Chladni figure, the sand lying on the plate's still lines.
                    float2 q = (p * 0.75 + 1) * 1.5708;
                    float n = 2 + floor(frac(t * 0.08) * 3), m = n + 2;
                    float f = cos(n * q.x) * cos(m * q.y) - cos(m * q.x) * cos(n * q.y);
                    e = smoothstep(0.22, 0.0, abs(f)) * step(max(abs(p.x), abs(p.y)), 0.62);
                }
                return saturate(e);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _Time.y;
                float2 p = input.uv * 2 - 1;
                float r = length(p);
                float z = sqrt(saturate(1 - r * r));
                float fresnel = pow(1 - z, 2.5);

                // Glass body with an iridescent, slowly turning rim.
                float hue = frac(atan2(p.y, p.x) / 6.2831 + t * 0.05 + fresnel * 0.7 + _Hover * 0.15);
                float3 rim = lerp(_Color.rgb, Hue(hue), 0.55);
                float3 color = lerp(_Color.rgb * 0.22, rim, fresnel);
                float alpha = 0.16 + fresnel * 0.75 + _Hover * 0.06;

                // Glossy highlights.
                float2 h1 = p - float2(-0.36, 0.42), h2 = p - float2(0.34, -0.48);
                float gloss = exp(-dot(h1, h1) * 38) * 0.85 + exp(-dot(h2, h2) * 110) * 0.3;
                color += gloss;
                alpha = max(alpha, gloss);

                float e = Emblem(p * 1.5, _Icon, t);
                color += lerp(_Color.rgb, 1, 0.35) * e * (1.1 + _Hover * 0.6);
                alpha = max(alpha, e * 0.95);

                // The current world gets a brighter ring.
                color += _Color.rgb * _Selected * smoothstep(0.86, 0.95, r) * 0.9;

                alpha *= _Alpha * smoothstep(1.0, 0.96, r);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
