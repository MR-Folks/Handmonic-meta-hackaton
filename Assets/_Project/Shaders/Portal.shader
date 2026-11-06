// The Finger frame window: a quad held between the hands that shows another world behind it. The world is drawn from
// the direction you look through the window, with its hills on planes a few metres to a few dozen metres away from
// where you stood (_Origin, facing _Forward, its horizon _Tilt below eye level), so it stays put in the room and shifts with parallax as you move the
// frame or your head, like a real hole in the wall. Four worlds (_SceneA, cross-fading into _SceneB with _Mix), each
// moving with the music: _Pulse on the beat, _Bands and _High the six bands. _Open opens the window from its middle,
// and its edge glows.
Shader "MusicInYourHands/Portal"
{
    Properties
    {
        _SceneA ("World (0 valley, 1 ocean, 2 neon city, 3 aurora)", Float) = 0
        _SceneB ("Next world", Float) = 1
        _Mix ("Change to the next world 0-1", Float) = 0
        _Open ("Open 0-1", Float) = 1
        _Size ("Window size (m)", Vector) = (0.3, 0.2, 0, 0)
        _Origin ("Where the world was anchored", Vector) = (0, 1.6, 0, 0)
        _Forward ("Which way it faces", Vector) = (0, 0, 1, 0)
        _Tilt ("Horizon below eye level (radians)", Float) = 0.3
        _Pulse ("Beat 0-1", Float) = 0
        _Bands ("Bands 0-3", Vector) = (0.5, 0.5, 0.5, 0.5)
        _High ("Bands 4-5", Vector) = (0.5, 0.5, 0, 0)
        _Clock ("Clock (s)", Float) = 0
        _EdgeColor ("Edge colour", Color) = (1, 0.9, 0.7, 1)
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
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SceneA;
                float _SceneB;
                float _Mix;
                float _Open;
                float4 _Size;
                float4 _Origin;
                float4 _Forward;
                float _Tilt;
                float _Pulse;
                float4 _Bands;
                float4 _High;
                float _Clock;
                float4 _EdgeColor;
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

            float Hash1(float n) { return frac(sin(n * 12.9898) * 43758.5453); }
            float Hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float x)
            {
                float i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(Hash1(i), Hash1(i + 1), f);
            }

            // A mountain skyline, 0-1.
            float Ridge(float x, float seed)
            {
                return Noise(x * 0.6 + seed) * 0.6 + Noise(x * 1.7 + seed * 3.1) * 0.28 + Noise(x * 4.3 + seed * 7.3) * 0.12;
            }

            float Band(int i)
            {
                return i == 0 ? _Bands.x : i == 1 ? _Bands.y : i == 2 ? _Bands.z : i == 3 ? _Bands.w : i == 4 ? _High.x : _High.y;
            }

            // The view ray in the world's own frame.
            struct View
            {
                float3 rel;   // the eye relative to where the world was anchored
                float3 dir;   // looking direction
                float3 fwd, right, up;
                float f, r, u; // the direction along forward, right and up
            };

            // Where the ray meets a layer standing <distance> m ahead of the anchor: x across and the height y, both
            // divided by the distance (roughly angles, so every layer uses the same numbers for its shapes).
            bool Layer(View v, float distance, out float2 at)
            {
                at = 0;
                if (v.f < 0.02) return false;
                float t = (distance - dot(v.rel, v.fwd)) / v.f;
                if (t <= 0) return false;
                float3 p = v.rel + v.dir * t;
                at = float2(dot(p, v.right), dot(p, v.up)) / distance;
                return true;
            }

            // Twinkling points scattered over the sky, one at most per grid cell.
            float Sparkles(float2 p, float cells, float share, float clock)
            {
                float2 g = p * cells, c = floor(g);
                float h = Hash2(c);
                if (h > share) return 0;
                float2 o = g - c - (0.25 + 0.5 * float2(Hash2(c + 3.1), Hash2(c + 7.7)));
                return exp(-dot(o, o) * 60) * (0.6 + 0.4 * sin(clock * (2 + h * 5) + h * 40));
            }

            // A dreamy valley at sunset: three ranges of soft hills, a sun that pulses on the beat, drifting clouds and
            // little lights rising out of the valley.
            float3 Valley(View v)
            {
                float u = v.u;
                float3 sky = lerp(float3(1.0, 0.68, 0.38), float3(1.0, 0.42, 0.62), saturate(u * 3 + 0.1));
                sky = lerp(sky, float3(0.42, 0.32, 0.88), saturate(u * 2 - 0.3));
                float3 sun = normalize(v.fwd + v.up * 0.1 + v.right * 0.08);
                float d = 1 - dot(v.dir, sun);
                float size = 0.0018 * (1 + _Pulse * 0.5);
                sky += float3(1, 0.85, 0.6) * exp(-d * 70) * (0.5 + _Pulse * 0.4);
                sky = lerp(sky, float3(1, 0.97, 0.85), smoothstep(size, size * 0.7, d));
                float cloud = exp(-pow((u - 0.2 - 0.04 * sin(v.r * 5)) * 30, 2)) * smoothstep(0.35, 0.8, Noise(v.r * 6 + _Clock * 0.04));
                sky = lerp(sky, float3(1, 0.88, 0.9), cloud * 0.55);
                sky += float3(1, 0.95, 0.8) * Sparkles(float2(v.r, u - _Clock * 0.01), 26, 0.25, _Clock) * saturate(u * 4) * 0.8;

                float3 color = sky;
                float2 at;
                // Each range darker at its foot and catching the sun along its ridge.
                if (Layer(v, 60, at))
                {
                    float top = 0.04 + 0.1 * Ridge(at.x * 2, 1);
                    if (at.y < top)
                        color = lerp(float3(0.6, 0.36, 0.85), sky, 0.3) * lerp(0.8, 1.05, smoothstep(top - 0.08, top, at.y))
                              + float3(1, 0.75, 0.55) * smoothstep(0.006, 0, top - at.y) * 0.35;
                }
                if (Layer(v, 18, at))
                {
                    float top = -0.05 + 0.08 * Ridge(at.x * 3, 5) + _Bands.y * 0.012 * _Pulse;
                    if (at.y < top)
                        color = lerp(float3(0.28, 0.32, 0.82), sky, 0.1) * lerp(0.7, 1.05, smoothstep(top - 0.07, top, at.y))
                              + float3(1, 0.7, 0.6) * smoothstep(0.005, 0, top - at.y) * 0.4;
                }
                if (Layer(v, 5, at))
                {
                    float top = -0.2 + 0.1 * Ridge(at.x * 4, 9) + _Bands.x * 0.02 * _Pulse;
                    if (at.y < top)
                    {
                        color = float3(0.24, 0.78, 0.62) * (0.8 + 0.2 * smoothstep(top - 0.06, top, at.y));
                        // Lights rising from the near hills on the beat.
                        color += float3(1, 0.9, 0.6) * Sparkles(at * float2(1, 3), 14, 0.12, _Clock) * (0.4 + _Pulse);
                    }
                }
                return color;
            }

            // Under the sea: light shafts from the surface, rising bubbles, glowing jellyfish that pulse on the beat and
            // ridges of the sea floor.
            float3 Ocean(View v)
            {
                float u = v.u;
                float3 color = lerp(float3(0.0, 0.1, 0.22), float3(0.05, 0.55, 0.72), saturate(u * 1.4 + 0.45));
                float shaft = pow(saturate(sin(v.r * 16 + sin(u * 3 + _Clock * 0.2) * 1.5 + _Clock * 0.15) * 0.5 + 0.5), 10);
                color += float3(0.5, 0.9, 1) * shaft * saturate(u + 0.25) * (0.22 + _High.x * 0.15);
                float caustic = Noise(v.r * 30 + _Clock * 0.7) * Noise(u * 40 - _Clock * 0.5);
                color += float3(0.6, 1, 1) * caustic * smoothstep(0.35, 0.6, u) * 0.3;

                // Bubbles: rings rising in columns.
                float2 b = float2(v.r * 14, u * 14 - _Clock * 0.9);
                float2 c = floor(b);
                float h = Hash2(c);
                float2 o = b - c - float2(0.5 + 0.3 * sin(_Clock + h * 9), 0.5);
                float ring = smoothstep(0.06, 0.0, abs(length(o) - 0.12 - h * 0.08));
                color += float3(0.8, 1, 1) * ring * step(h, 0.3) * 0.6;

                // Jellyfish: soft glowing domes drifting slowly.
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float2 j = float2(-0.25 + k * 0.25 + 0.05 * sin(_Clock * 0.3 + k), 0.1 + 0.08 * sin(_Clock * 0.4 + k * 2));
                    float2 q = (float2(v.r, u) - j) * float2(1, 1.6);
                    float glow = exp(-dot(q, q) * 900) * (0.6 + _Pulse * 0.8) + exp(-dot(q, q) * 120) * 0.15;
                    color += lerp(float3(1, 0.5, 0.85), float3(0.6, 0.7, 1), k * 0.5) * glow;
                }

                float2 at;
                if (Layer(v, 30, at) && at.y < -0.05 + 0.06 * Ridge(at.x * 2.5, 2)) color = lerp(color, float3(0.03, 0.25, 0.35), 0.75);
                if (Layer(v, 10, at) && at.y < -0.13 + 0.07 * Ridge(at.x * 3.5, 6) + _Bands.y * 0.01 * _Pulse) color = float3(0.04, 0.33, 0.4);
                if (Layer(v, 4, at))
                {
                    float top = -0.3 + 0.1 * Ridge(at.x * 5, 11);
                    // Sea grass swaying with the music.
                    float blade = frac(at.x * 40);
                    float tall = top + 0.12 * Hash1(floor(at.x * 40)) * (0.6 + _Bands.z * 0.6);
                    float sway = 0.02 * sin(_Clock * 1.5 + at.x * 8) * (at.y - top) * 10;
                    if (at.y < top) color = float3(0.06, 0.45, 0.45);
                    else if (at.y < tall && abs(blade - 0.5 - sway) < 0.12) color = float3(0.1, 0.65, 0.45);
                }
                return color;
            }

            // A neon city at night: a striped sunset sun, a skyline whose towers rise with the six bands, lit windows,
            // and a glowing grid floor running towards you on the beat.
            float3 City(View v)
            {
                float u = v.u;
                float3 color = lerp(float3(0.9, 0.22, 0.5), float3(0.06, 0.02, 0.16), saturate(u * 3));
                color += float3(1, 1, 1) * Sparkles(float2(v.r, u), 40, 0.25, _Clock) * saturate(u * 3 - 0.3) * 0.7;
                float2 s = float2(v.r, u - 0.07) * float2(1, 1.1);
                float sun = smoothstep(0.17, 0.165, length(s));
                float stripes = step(0.42, frac((u - 0.07) * 36 + _Clock * 0.4)) + step(0.02, u - 0.07);
                color = lerp(color, lerp(float3(1, 0.3, 0.55), float3(1, 0.88, 0.25), saturate(s.y * 4 + 0.5)), sun * saturate(stripes));
                color += float3(1, 0.4, 0.6) * exp(-length(s) * 9) * 0.35 * (1 + _Pulse);

                float2 at;
                if (Layer(v, 25, at) && at.y > -0.06)
                {
                    float col = floor(at.x * 16);
                    float tower = 0.02 + 0.12 * Hash1(col) * (0.45 + Band((int)abs(col) % 6) * 0.9);
                    if (at.y < tower)
                    {
                        color = float3(0.07, 0.03, 0.14);
                        float2 w = float2(frac(at.x * 16 * 4), frac(at.y * 16 * 6));
                        float lit = step(0.55, Hash2(floor(float2(at.x * 64, at.y * 96))));
                        float window = step(0.3, w.x) * step(w.x, 0.75) * step(0.3, w.y) * step(w.y, 0.7) * lit;
                        color += lerp(float3(0, 0.9, 1), float3(1, 0.3, 0.8), Hash1(col * 3.7)) * window * (0.7 + _Pulse * 0.6);
                        color += float3(1, 0.2, 0.7) * smoothstep(0.003, 0, abs(at.y - tower)) * 0.8;
                    }
                }
                if (u < -0.005)
                {
                    // The grid floor 1.4 m below the eye.
                    float t = (-1.4 - dot(v.rel, v.up)) / u;
                    float3 p = v.rel + v.dir * t;
                    float gx = abs(frac(dot(p, v.right) * 0.5) - 0.5);
                    float gz = abs(frac(dot(p, v.fwd) * 0.5 - _Clock * 0.6) - 0.5);
                    float width = 0.02 + t * 0.002;
                    float gridLine = smoothstep(width, 0, min(gx, gz));
                    float fade = exp(-t * 0.03);
                    color = lerp(float3(0.05, 0.0, 0.1), float3(1, 0.2, 0.8), gridLine * fade * (0.6 + _Pulse * 0.6));
                    color += float3(1, 0.3, 0.6) * exp(u * 30) * 0.4;
                }
                return color;
            }

            // An arctic night: stars, an aurora whose curtains wave and brighten with the music, snowy peaks and a still
            // lake mirroring it all.
            float3 AuroraSky(float r, float u)
            {
                float3 color = lerp(float3(0.03, 0.06, 0.14), float3(0.01, 0.01, 0.05), saturate(u * 2));
                color += float3(0.9, 0.95, 1) * Sparkles(float2(r, u), 50, 0.35, _Clock) * 0.9;
                float wave = sin(r * 5 + _Clock * 0.2 + sin(r * 13 + _Clock * 0.5)) * 0.07;
                float band = exp(-pow((u - 0.28 - wave) * 7, 2)) * (0.55 + 0.45 * sin(r * 45 + _Clock * 0.8 + sin(r * 9) * 3));
                float energy = 0.6 + _Bands.z * 0.6 + _Pulse * 0.5;
                color += lerp(float3(0.2, 1, 0.55), float3(0.7, 0.35, 1), saturate((u - 0.2) * 4)) * band * energy * smoothstep(0.02, 0.15, u);
                return color;
            }

            float3 Aurora(View v)
            {
                float u = v.u;
                bool lake = u < 0;
                float3 color = AuroraSky(v.r, abs(u) + (lake ? 0.003 * sin(v.r * 300 + _Clock * 2) : 0));
                if (lake) color = color * 0.55 + float3(0.01, 0.03, 0.06);
                float2 at;
                if (Layer(v, 40, at) && at.y < 0.1 * Ridge(at.x * 2.2, 3) && at.y > 0)
                {
                    float top = 0.1 * Ridge(at.x * 2.2, 3);
                    color = lerp(float3(0.1, 0.13, 0.24), float3(0.75, 0.82, 0.95), smoothstep(top - 0.035, top - 0.005, at.y));
                }
                if (Layer(v, 12, at) && at.y > -0.005 && at.y < 0.035 * Ridge(at.x * 4, 8))
                    color = float3(0.04, 0.06, 0.12);
                return color;
            }

            float3 Scene(float scene, View v)
            {
                if (scene < 0.5) return Valley(v);
                if (scene < 1.5) return Ocean(v);
                if (scene < 2.5) return City(v);
                return Aurora(v);
            }

            float RoundedBox(float2 p, float2 extent, float radius)
            {
                float2 q = abs(p) - extent + radius;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                View v;
                float3 eye = GetCameraPositionWS();
                v.dir = normalize(input.positionWS - eye);
                v.rel = eye - _Origin.xyz;
                // The world's horizon sits a little below eye level, where hands usually make the frame.
                float3 level = normalize(float3(_Forward.x, 0, _Forward.z) + float3(0, 0, 1e-4));
                v.right = normalize(cross(float3(0, 1, 0), level));
                v.fwd = level * cos(_Tilt) - float3(0, 1, 0) * sin(_Tilt);
                v.up = float3(0, 1, 0) * cos(_Tilt) + level * sin(_Tilt);
                v.f = dot(v.dir, v.fwd);
                v.r = dot(v.dir, v.right);
                v.u = dot(v.dir, v.up);

                // The window opens from its middle; q is the point on it in metres from the centre.
                float2 q = (input.uv - 0.5) * _Size.xy;
                float2 extent = _Size.xy * 0.5 * _Open;
                float d = RoundedBox(q, extent, min(0.012, min(extent.x, extent.y)));
                float inside = smoothstep(0.0015, -0.0015, d) * step(0.001, _Open);

                float3 color = 0;
                if (inside > 0)
                {
                    color = Scene(_SceneA, v);
                    if (_Mix > 0.001)
                    {
                        // The next world swirls in from the middle.
                        float2 p = input.uv - 0.5;
                        float rr = length(p) * 1.4 + 0.08 * sin(atan2(p.y, p.x) * 5 + _Clock * 4);
                        float m = smoothstep(rr - 0.12, rr + 0.12, _Mix * 1.35);
                        if (m > 0.001) color = lerp(color, Scene(_SceneB, v), m);
                        color += _EdgeColor.rgb * exp(-abs(_Mix * 1.35 - rr) * 25) * (1 - _Mix) * 1.5;
                    }
                    // A little shade just inside the rim, so the window reads as having depth.
                    color *= lerp(0.7, 1, smoothstep(0, 0.02, -d));
                }

                // The glowing rim, with sparks running round it.
                float rim = exp(-abs(d) / 0.0035) * (0.55 + _Pulse * 0.6) * step(0.001, _Open);
                float around = atan2(q.y / max(_Size.y, 1e-3), q.x / max(_Size.x, 1e-3));
                float sparks = pow(saturate(sin(around * 9 - _Clock * 3)), 24) * exp(-abs(d) / 0.006) * 1.2 * step(0.001, _Open);
                float glow = rim + sparks;
                float3 rgb = color * inside + _EdgeColor.rgb * glow;
                float alpha = saturate(inside + glow * 0.7);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
