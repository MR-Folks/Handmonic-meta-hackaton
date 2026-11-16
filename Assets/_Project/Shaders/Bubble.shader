// A soap-bubble button for the world menu, drawn on a camera-facing quad: a clear glass body, an iridescent rim,
// a glossy highlight and a small animated emblem of the world inside. _Icon picks the emblem (-1 for none, as the
// Bubble notes world uses it for its plain bubbles).
Shader "MusicInYourHands/Bubble"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.37, 0.68, 1)
        _Icon ("Emblem (-1 none, 0-30)", Float) = 0
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

            float Segment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                return length(pa - ba * saturate(dot(pa, ba) / dot(ba, ba)));
            }

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
                    // Air strings: a small harp, one string ringing.
                    [unroll] for (int k = 0; k < 5; k++)
                    {
                        float x = -0.44 + k * 0.22 + (k == 2 ? 0.05 * sin(p.y * 9 + t * 14) * (1 - p.y * p.y) : 0);
                        float top = 0.62 - abs(k - 2) * 0.08;
                        e += Line(abs(p.x - x), 0.03) * step(p.y, top) * step(-0.62, p.y);
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
                    // Sound wind: a school of small fish swimming in a ring.
                    [unroll] for (int k = 0; k < 9; k++)
                    {
                        float a = k * 0.698 + t * 0.9;
                        float r = 0.42 + 0.08 * sin(k * 2.3);
                        float2 c = float2(cos(a), sin(a) * 0.75) * r;
                        float2 dir = normalize(float2(-sin(a), cos(a) * 0.75));
                        float2 q = p - c;
                        float along = dot(q, dir), across = dot(q, float2(-dir.y, dir.x));
                        e += smoothstep(0.03, 0.0, abs(across) - 0.012 * (1 - abs(along) / 0.09)) * step(abs(along), 0.09);
                    }
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
                else if (icon < 11.5)
                {
                    // Sand table: a Chladni figure (mode 3:5), the sand lying on the plate's still lines, in a plate.
                    float2 q = (p / 0.6 + 1) * 1.5708;
                    float f = cos(3 * q.x) * cos(5 * q.y) - cos(5 * q.x) * cos(3 * q.y);
                    float inPlate = step(max(abs(p.x), abs(p.y)), 0.6);
                    e = smoothstep(0.35, 0.0, abs(f)) * inPlate * (0.75 + 0.25 * sin(t * 2));
                    e += Line(abs(max(abs(p.x), abs(p.y)) - 0.62), 0.03) * 0.7;
                }
                else if (icon < 12.5)
                {
                    // Orbit type: a giant cropped letter A with a thin orbit and a turning ring of dots.
                    float2 q = p - float2(-0.1, -0.15);
                    float legs = min(abs(q.x - (0.45 - q.y) * 0.38), abs(q.x + (0.45 - q.y) * 0.38)) - 0.07;
                    float letter = step(legs, 0) * step(q.y, 0.45) * step(-0.75, q.y) + step(abs(q.y + 0.25), 0.05) * step(abs(q.x), 0.25);
                    e = saturate(letter) * step(p.y, 0.62);
                    e += Line(abs(length(p - float2(0.25, 0.3)) - 0.3), 0.025) * 0.8;
                    float2 r = p - float2(0.35, -0.2);
                    float a = atan2(r.y, r.x) + t;
                    e += step(abs(length(r) - 0.16), 0.03) * step(0.6, frac(a * 12 / 6.2832));
                }
                else if (icon < 13.5)
                {
                    // Graffiti: a quick spray-painted squiggle with drips.
                    float y = 0.12 * sin(p.x * 7 + 1.0) + 0.05;
                    float stroke = step(abs(p.x), 0.6) * smoothstep(0.1, 0.05, abs(p.y - y));
                    float drips = 0;
                    [unroll] for (int k = 0; k < 3; k++)
                    {
                        float x = -0.35 + k * 0.32;
                        float top = 0.12 * sin(x * 7 + 1.0) + 0.05;
                        float len = 0.2 + 0.15 * frac(k * 0.37 + t * 0.2);
                        drips += step(abs(p.x - x), 0.025) * step(p.y, top) * step(top - len, p.y);
                        drips += exp(-dot(p - float2(x, top - len), p - float2(x, top - len)) * 900);
                    }
                    e = stroke + drips;
                }
                else if (icon < 15.5)
                {
                    // Next song (14) or previous song (15, mirrored): two arrowheads and a bar, nudging forward.
                    float2 q = float2(icon > 14.5 ? -p.x : p.x, p.y);
                    q.x -= 0.04 * (0.5 + 0.5 * sin(t * 3));
                    [unroll] for (int k = 0; k < 2; k++)
                    {
                        float x0 = -0.44 + k * 0.34;
                        e += step(x0, q.x) * step(abs(q.y), 0.3 * saturate((x0 + 0.34 - q.x) / 0.34));
                    }
                    e += step(abs(q.x - 0.32), 0.045) * step(abs(q.y), 0.3);
                }
                else if (icon < 16.5)
                {
                    // Breathing room: a room in perspective, its back wall breathing, a wave on the floor.
                    float s = 0.55 + 0.05 * sin(t * 2.5);
                    float back = 0.24 + 0.03 * sin(t * 2.5);
                    e = Line(abs(max(abs(p.x), abs(p.y)) - s), 0.025) * 0.6 + Line(abs(max(abs(p.x), abs(p.y)) - back), 0.025);
                    [unroll] for (int k = 0; k < 4; k++)
                    {
                        float2 c = float2(k % 2 == 0 ? -1 : 1, k < 2 ? -1 : 1);
                        e += Line(Segment(p, c * back, c * s), 0.02) * 0.6;
                    }
                    float2 f = float2(p.x, (p.y + 0.42) * 3);
                    e += pow(abs(sin(length(f) * 8 - t * 3)), 12) * step(p.y, -back) * step(abs(p.x), s) * 0.7;
                }
                else if (icon < 17.5)
                {
                    // Light trails: a looping ribbon drawn in the air.
                    float d = 9;
                    [unroll] for (int k = 0; k < 24; k++)
                    {
                        float a0 = k / 24.0 * 6.2832, a1 = (k + 1) / 24.0 * 6.2832;
                        float2 c0 = float2(sin(a0) * 0.5, sin(a0 * 2) * 0.28), c1 = float2(sin(a1) * 0.5, sin(a1 * 2) * 0.28);
                        d = min(d, Segment(p, c0, c1));
                    }
                    e = Line(d, 0.05 + 0.015 * sin(t * 4));
                }
                else if (icon < 18.5)
                {
                    // Neon sign: a framed sign, its words flickering on.
                    float2 q = abs(p) - float2(0.55, 0.32);
                    e = Line(abs(length(max(q, 0)) + min(max(q.x, q.y), 0) - 0.06), 0.03) * 0.8;
                    [unroll] for (int k = 0; k < 3; k++)
                    {
                        float x = -0.32 + k * 0.32;
                        float on = step(0.3, frac(t * 0.6 + k * 0.33)) * (0.75 + 0.25 * step(0.5, frac(t * 13 + k)));
                        e += Line(Segment(p, float2(x - 0.1, -0.02), float2(x + 0.1, 0.02)), 0.045) * on;
                    }
                }
                else if (icon < 19.5)
                {
                    // Word drift: words coming towards you out of the distance, growing.
                    [unroll] for (int k = 0; k < 3; k++)
                    {
                        float u = frac(t * 0.3 + k / 3.0);
                        float2 c = lerp(float2(0.0, 0.1), float2((k - 1) * 0.55, -0.1 + (k % 2) * 0.35), u);
                        float w = lerp(0.06, 0.3, u), h = lerp(0.02, 0.09, u);
                        float2 q = abs(p - c) - float2(w, h);
                        e += step(max(q.x, q.y), 0) * (1 - u * 0.4) * smoothstep(1, 0.8, u);
                    }
                }
                else if (icon < 20.5)
                {
                    // Rising floor: a word standing up out of the floor, a ripple round its feet.
                    float rise = 0.5 + 0.5 * sin(t * 1.5);
                    float2 q = abs(p - float2(0, -0.2 + rise * 0.2)) - float2(0.32, 0.12);
                    e = step(max(q.x, q.y), 0) * step(-0.25, p.y);
                    float2 r = float2(p.x, (p.y + 0.28) * 3.5);
                    e += Line(abs(length(r) - 0.45 - 0.1 * frac(t)), 0.05) * (1 - frac(t));
                    e += Line(abs(p.y + 0.25), 0.015) * step(abs(p.x), 0.7) * 0.5;
                }
                else if (icon < 21.5)
                {
                    // Word snow: little words and flakes falling.
                    [unroll] for (int k = 0; k < 6; k++)
                    {
                        float x = -0.5 + k * 0.2;
                        float y = 0.6 - frac(t * 0.2 + k * 0.37) * 1.2;
                        float2 q = p - float2(x + 0.04 * sin(t + k), y);
                        if (k % 2 == 0) e += step(max(abs(q.x) - 0.1, abs(q.y) - 0.03), 0);
                        else e += exp(-dot(q, q) * 600);
                    }
                }
                else if (icon < 22.5)
                {
                    // Ceiling stars: a small constellation, joined by lines, twinkling.
                    float2 s0 = float2(-0.5, -0.1), s1 = float2(-0.18, 0.25), s2 = float2(0.12, 0.05), s3 = float2(0.45, 0.32), s4 = float2(0.3, -0.35);
                    e = Line(Segment(p, s0, s1), 0.015) * 0.5 + Line(Segment(p, s1, s2), 0.015) * 0.5
                      + Line(Segment(p, s2, s3), 0.015) * 0.5 + Line(Segment(p, s2, s4), 0.015) * 0.5;
                    float2 ss[5] = { s0, s1, s2, s3, s4 };
                    [unroll] for (int k = 0; k < 5; k++)
                    {
                        float2 q = p - ss[k];
                        e += exp(-dot(q, q) * 250) * (0.7 + 0.3 * sin(t * 3 + k * 2));
                    }
                }
                else if (icon < 23.5)
                {
                    // Lyrics: three lines of text, a word lighting up along them.
                    [unroll] for (int k = 0; k < 3; k++)
                    {
                        float y = 0.3 - k * 0.3;
                        float width = k == 2 ? 0.3 : 0.5;
                        float bar = step(abs(p.x + 0.5 - width), width) * step(abs(p.y - y), 0.055);
                        e += bar * (0.55 + 0.45 * step(frac(t * 0.4 - k * 0.33), 0.33));
                    }
                }
                else if (icon < 24.5)
                {
                    // The cassette deck: a cassette with a label band along its top and its two reels slowly turning.
                    float2 q = abs(p - float2(0, 0.02)) - float2(0.56, 0.34);
                    float shell = length(max(q, 0)) + min(max(q.x, q.y), 0) - 0.1;
                    e = Line(abs(shell), 0.035);
                    e += step(abs(p.x), 0.44) * step(abs(p.y - 0.25), 0.065) * 0.5;
                    [unroll] for (int k = 0; k < 2; k++)
                    {
                        float2 c = p - float2(k == 0 ? -0.25 : 0.25, -0.07);
                        float d = length(c);
                        e += Line(abs(d - 0.13), 0.028);
                        float a = atan2(c.y, c.x) - t * 1.1;
                        e += pow(abs(cos(a * 1.5)), 30) * step(0.035, d) * step(d, 0.12) * 0.85;
                    }
                    e += Line(Segment(p, float2(-0.25, 0.06), float2(0.25, 0.06)), 0.02) * 0.35;
                    e += Line(Segment(p, float2(-0.32, -0.42), float2(-0.22, -0.3)), 0.022) * 0.6
                       + Line(Segment(p, float2(-0.22, -0.3), float2(0.22, -0.3)), 0.022) * 0.6
                       + Line(Segment(p, float2(0.22, -0.3), float2(0.32, -0.42)), 0.022) * 0.6;
                }
                else if (icon < 25.5)
                {
                    // BEGIN emblem 25 (Solar system)
                    // Solar system: you are the small dot in the middle, with planets going round you on tipped orbits,
                    // and the sun, a bright disc in its glow, drifts slowly round the outer ring, floating a little above it.
                    e = smoothstep(0.075, 0.045, length(p));
                    [unroll] for (int k = 0; k < 3; k++)
                    {
                        float r = 0.3 + k * 0.21;
                        float2 q = float2(p.x, p.y / 0.5);
                        float stretch = length(float2(p.x, p.y / 0.25)) / max(length(q), 1e-3);
                        e += Line(abs(length(q) - r) / max(stretch, 1e-3), 0.03) * 0.5;
                        if (k < 2)
                        {
                            float a = t * (1.1 - k * 0.45) + k * 2.6;
                            float2 c = float2(cos(a), sin(a) * 0.5) * r;
                            float size = 0.06 + k * 0.018;
                            e += smoothstep(size, size - 0.03, length(p - c));
                        }
                    }
                    float sa = t * 0.35 + 0.8;
                    float2 sc = float2(cos(sa), sin(sa) * 0.5) * 0.72 + float2(0, 0.1);
                    float sd = length(p - sc);
                    e += smoothstep(0.14, 0.1, sd) + exp(-sd * sd * 30) * (0.45 + 0.15 * sin(t * 3.0));
                    // END emblem 25
                }
                else if (icon < 26.5)
                {
                    // BEGIN emblem 26 (Flower dance)
                    // A flower bopping from side to side on the beat, its leaves lifting like arms; every fourth beat
                    // its head pops open bigger.
                    float2 g = (p - float2(0, 0.06)) / 1.2;
                    float beat = t * 1.6;
                    float hit = frac(beat);
                    float lean = (fmod(floor(beat), 2) < 0.5 ? 1 : -1) * cos(3.1416 * smoothstep(0.1, 0.9, hit));
                    float pop = 1 + 0.22 * exp(-frac(beat * 0.25) * 10);
                    float2 head = float2(lean * 0.2, 0.17 - 0.05 * exp(-hit * 7));
                    // The stem: a curve from the ground, upright at first, bending over to the head.
                    float stem = 9;
                    float2 s0 = float2(0, -0.72);
                    [unroll] for (int k = 1; k <= 6; k++)
                    {
                        float u = k / 6.0;
                        float2 s1 = float2(head.x * u * u, lerp(-0.72, head.y, u));
                        stem = min(stem, Segment(g, s0, s1));
                        s0 = s1;
                    }
                    e = Line(stem, 0.032);
                    // Two leaves low on the stem, lifting on the beat.
                    float lift = 0.35 + 0.45 * exp(-hit * 4);
                    [unroll] for (int j = 0; j < 2; j++)
                    {
                        float side = j == 0 ? -1 : 1;
                        float y = -0.56 + j * 0.1;
                        float2 root = float2(head.x * pow((y + 0.72) / (head.y + 0.72), 2), y);
                        float2 axis = float2(side * cos(lift), sin(lift));
                        float2 q = g - root - axis * 0.13;
                        float along = dot(q, axis), across = dot(q, float2(-axis.y, axis.x));
                        e += smoothstep(1.0, 0.8, length(float2(along / 0.13, across / 0.06)));
                    }
                    // The head: five round petals, a ring of space, and the centre.
                    float2 c = g - head;
                    float petals = 0;
                    [unroll] for (int m = 0; m < 5; m++)
                    {
                        float a = m * 1.2566 + 1.5708 + lean * 0.35;
                        petals = max(petals, smoothstep(0.12, 0.095, length(c - float2(cos(a), sin(a)) * 0.15 * pop) / pop));
                    }
                    float r = length(c) / pop;
                    e = max(saturate(e), max(petals * smoothstep(0.075, 0.1, r), smoothstep(0.07, 0.05, r)));
                    // END emblem 26
                }
                else if (icon < 27.5)
                {
                    // BEGIN emblem 27 (Finger frame)
                    // Two L-shaped hands framing a window, a sun rising and setting inside it.
                    float2 q = abs(p) - float2(0.42, 0.28);
                    e = Line(abs(max(q.x, q.y)), 0.02) * 0.45;
                    e += Line(Segment(p, float2(-0.5, -0.36), float2(-0.5, 0.2)), 0.05) + Line(Segment(p, float2(-0.5, -0.36), float2(0.1, -0.36)), 0.05);
                    e += Line(Segment(p, float2(0.5, 0.36), float2(0.5, -0.2)), 0.05) + Line(Segment(p, float2(0.5, 0.36), float2(-0.1, 0.36)), 0.05);
                    float2 sun = p - float2(0, -0.05 + 0.1 * sin(t * 1.5));
                    e += step(length(sun), 0.12) * step(-0.2, p.y) * 0.9;
                    // END emblem 27
                }
                else if (icon < 28.5)
                {
                    // BEGIN emblem 28 (Earth and water)
                    // Water and earth in turn, each fading into the other: a drop of water and a rock with two peaks.
                    // Two dots beside it, the drop's on the left and the rock's on the right, show which is up.
                    float phase = t / 1.6;
                    float blend = smoothstep(0.8, 1.0, frac(phase));
                    float2 show = fmod(floor(phase), 2) < 0.5 ? float2(1 - blend, blend) : float2(blend, 1 - blend);
                    // A drop of water with a ripple spreading under it.
                    float2 gd = p - float2(0, -0.16);
                    float ud = gd.y / 0.62;
                    float bottom = saturate(1 - ud / 0.42);
                    float drop = smoothstep(0.015, -0.01, abs(gd.x) - 0.25 * (ud < 0.42 ? sqrt(1 - bottom * bottom) : pow(saturate((1 - ud) / 0.58), 0.85))) * step(0, ud) * step(ud, 1);
                    float2 shine = (p - float2(-0.1, 0.13)) * float2(1.6, 1);
                    drop *= 1 - 0.8 * smoothstep(0.015, 0, length(shine) - 0.05);
                    float2 rr = (p - float2(0, -0.3)) * float2(1, 3.2);
                    drop = max(drop, Line(abs(length(rr) - 0.32 - 0.04 * frac(t)), 0.06) * step(0.03, abs(rr.x)) * (1 - frac(t) * 0.5));
                    // A rock with two peaks.
                    float peaks = max(0.36 - abs(p.x + 0.08) * 1.15, 0.13 - abs(p.x - 0.25) * 1.15) - p.y;
                    float rock = smoothstep(-0.01, 0.02, min(peaks, p.y + 0.3));
                    e = drop * show.x + rock * show.y;
                    // The two dots: water on the left, earth on the right.
                    [unroll] for (int j = 0; j < 2; j++)
                    {
                        float2 d = p - float2(j == 0 ? -0.68 : 0.68, 0);
                        e += smoothstep(0.05, 0.03, length(d)) * (0.35 + 0.65 * show[j]);
                    }
                    // END emblem 28
                }
                else if (icon < 29.5)
                {
                    // BEGIN emblem 29 (Lightning)
                    // A bolt striking down, flickering, a ring spreading where it lands.
                    float d = min(min(Segment(p, float2(0.12, 0.6), float2(-0.12, 0.08)), Segment(p, float2(-0.12, 0.08), float2(0.14, 0.02))),
                                  Segment(p, float2(0.14, 0.02), float2(-0.08, -0.6)));
                    e = Line(d, 0.06) * (0.75 + 0.25 * step(0.5, frac(t * 7)));
                    e += Line(abs(length(float2(p.x * 0.6, (p.y + 0.62) * 2.5)) - 0.25 - 0.1 * frac(t)), 0.03) * (1 - frac(t));
                    // END emblem 29
                }
                else
                {
                    // BEGIN emblem 30 (Shadow play)
                    // A lamp's pool of light on a wall, a hand's shadow waving in it.
                    e = exp(-dot(p, p) * 2.5) * 0.55;
                    float wave = 0.15 * sin(t * 2);
                    float hand = step(length((p - float2(wave, -0.12)) * float2(1, 1.2)), 0.2);
                    [unroll] for (int k = 0; k < 4; k++)
                        hand = max(hand, step(Segment(p, float2(wave - 0.15 + k * 0.1, 0), float2(wave - 0.2 + k * 0.13, 0.35 - abs(k - 1.5) * 0.06)), 0.04));
                    hand = max(hand, step(Segment(p, float2(wave + 0.15, -0.1), float2(wave + 0.35, 0.08)), 0.045));
                    e *= 1 - hand;
                    e += Line(abs(length(p) - 0.62), 0.02) * 0.4;
                    // END emblem 30
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

                float e = Emblem(p * 1.22, _Icon, t);
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
