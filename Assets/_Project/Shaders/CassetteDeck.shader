// The cassette deck's solid parts: the table, the turntable, the radio and the cassettes. One shader for all of them,
// lit by a soft light from above and the viewer's side (the deck sits in your real room, so it carries its own light),
// with a gloss highlight and a faint rim that keeps edges readable against passthrough. _Style picks a surface:
//   0 glossy plastic, 1 wood (grain along the part's X), 2 speaker (cone and perforated grille, facing -Z),
//   3 paper label (UV 0-1 over the label: a stripe in _Accent, faint writing lines, a music note when _Mark is 1),
//   4 turntable (a rubber mat with fine rings on top, brushed metal round the edge), 5 indicator light,
//   6 brushed metal, 7 key (a symbol on its top face, _Icon: 1 play/pause, 2 stop, 3 eject, 4 close, 5 list,
//   6 plus), 8 reel hub (white teeth round a dark spindle, facing -Z).
// _Tint, _Accent, _Glow, _GlowColor and _Mark are set per part through a MaterialPropertyBlock (a cassette's colour,
// a hover glow, the light coming on). _Size gives the part's size in metres for the patterns. Everything below
// _CassetteDeckClip.x (a world height, when .y is 1) is cut away, so the deck can rise out of the floor.
// Writes alpha 1, so passthrough never shows through it.
Shader "MusicInYourHands/CassetteDeck"
{
    Properties
    {
        _Color ("Base colour", Color) = (0.8, 0.8, 0.8, 1)
        _Tint ("Tint (set per part)", Color) = (1, 1, 1, 1)
        _Accent ("Accent (set per part)", Color) = (1, 0.45, 0.55, 1)
        _GlowColor ("Glow colour (set per part)", Color) = (1, 1, 1, 1)
        _Glow ("Glow 0-1 (set per part)", Float) = 0
        _Gloss ("Gloss", Range(2, 160)) = 40
        _Shine ("Highlight strength", Range(0, 1.5)) = 0.4
        _Style ("Style (see the shader's comment)", Float) = 0
        _Mark ("Mark 0-1 (set per part)", Float) = 0
        _Icon ("Key symbol (set per part)", Float) = 0
        _Size ("Part size (m), for the patterns", Vector) = (0.1, 0.1, 0.1, 0)
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
                float4 _Color;
                float4 _Tint;
                float4 _Accent;
                float4 _GlowColor;
                float _Glow;
                float _Gloss;
                float _Shine;
                float _Style;
                float _Mark;
                float _Icon;
                float4 _Size;
            CBUFFER_END

            // Set by the deck while it rises out of the floor or sinks back (x: world height, y: 1 to cut).
            float4 _CassetteDeckClip;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                float2 uv : TEXCOORD4;
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

            // Distance to a segment, for the key symbols.
            float Segment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
                return length(pa - ba * h);
            }

            // Inside a triangle (counter-clockwise a, b, c), as a signed distance (negative inside).
            float Triangle(float2 p, float2 a, float2 b, float2 c)
            {
                float2 e0 = b - a, e1 = c - b, e2 = a - c;
                float d0 = (p.x - a.x) * e0.y - (p.y - a.y) * e0.x;
                float d1 = (p.x - b.x) * e1.y - (p.y - b.y) * e1.x;
                float d2 = (p.x - c.x) * e2.y - (p.y - c.y) * e2.x;
                return max(max(d0 / length(e0), d1 / length(e1)), d2 / length(e2));
            }

            float Box(float2 p, float2 h)
            {
                float2 d = abs(p) - h;
                return length(max(d, 0)) + min(max(d.x, d.y), 0);
            }

            // A key's symbol in [-1, 1] across its top face: 1 inside, 0 outside, softened over w.
            float Symbol(float2 p, float icon, float w)
            {
                float d = 1;
                if (icon < 1.5)
                {
                    // Play and pause: a triangle and two bars.
                    d = min(Triangle(p, float2(-0.55, -0.42), float2(0.0, 0.0), float2(-0.55, 0.42)), Box(p - float2(0.22, 0), float2(0.07, 0.38)));
                    d = min(d, Box(p - float2(0.48, 0), float2(0.07, 0.38)));
                }
                else if (icon < 2.5) d = Box(p, float2(0.36, 0.36));
                else if (icon < 3.5) d = min(Triangle(p, float2(-0.42, -0.02), float2(0.42, -0.02), float2(0, 0.42)), Box(p - float2(0, -0.32), float2(0.42, 0.08)));
                else if (icon < 4.5) d = min(Segment(p, float2(-0.4, -0.4), float2(0.4, 0.4)), Segment(p, float2(-0.4, 0.4), float2(0.4, -0.4))) - 0.11;
                else if (icon < 5.5)
                {
                    d = Box(p - float2(0, 0.34), float2(0.46, 0.07));
                    d = min(d, Box(p, float2(0.46, 0.07)));
                    d = min(d, Box(p + float2(0, 0.34), float2(0.46, 0.07)));
                }
                else d = min(Box(p, float2(0.45, 0.09)), Box(p, float2(0.09, 0.45)));
                return 1 - smoothstep(-w, w, d);
            }

            // A small music note on the label, at q (metres from the label's centre), for songs with lyrics.
            float Note(float2 q, float s)
            {
                float2 p = q / s;
                float head = length((p - float2(-0.18, -0.3)) * float2(1, 1.35)) - 0.24;
                float stem = Box(p - float2(0.04, 0.12), float2(0.045, 0.42));
                float flag = Segment(p, float2(0.04, 0.52), float2(0.36, 0.2)) - 0.06;
                float d = min(min(head, stem), flag);
                return 1 - smoothstep(-0.05, 0.05, d);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.uv = input.uv;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_CassetteDeckClip.y > 0.5) clip(input.positionWS.y - _CassetteDeckClip.x);

                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 p = input.positionOS;
                float3 no = normalize(input.normalOS);
                float3 albedo = _Color.rgb * _Tint.rgb;
                float gloss = _Gloss, shine = _Shine;
                float3 emit = 0;
                float style = _Style;

                if (style > 0.5 && style < 1.5)
                {
                    // Wood: long grain along X with slow waves and darker rings, a little lighter on the edges.
                    float wave = Noise3(p * float3(3, 6, 9)) * 2.2 + Noise3(p * float3(1, 2, 30)) * 0.4;
                    float grain = frac(p.z * 38 + wave);
                    float ring = smoothstep(0.0, 0.18, grain) * smoothstep(1.0, 0.55, grain);
                    float fine = Noise3(float3(p.x * 6, p.y * 160, p.z * 380));
                    float3 light = albedo * 1.18, dark = albedo * 0.62;
                    albedo = lerp(dark, light, ring * 0.7 + fine * 0.3);
                    albedo *= lerp(0.82, 1.0, saturate(no.y));
                }
                else if (style > 1.5 && style < 2.5)
                {
                    // Speaker: a dark cone with soft ridges round a glossy dust cap; on the beat a ring of light runs
                    // round its outer edge.
                    float r = length(p.xy) / max(_Size.x * 0.5, 1e-4);
                    float ridges = 0.82 + 0.18 * cos(r * 30);
                    float cap = smoothstep(0.3, 0.26, r);
                    albedo = lerp(albedo * ridges, albedo * 1.35 + 0.03, cap);
                    gloss = lerp(gloss, 90, cap);
                    shine = lerp(shine, 0.8, cap);
                    emit += _GlowColor.rgb * _Glow * (smoothstep(0.8, 0.9, r) * smoothstep(1.0, 0.93, r) * 2.6 + cap * 0.6);
                }
                else if (style > 2.5 && style < 3.5)
                {
                    // Paper label: a stripe of the cassette's colour along the top, faint lines, and a note for lyrics.
                    float2 uv = input.uv;
                    float2 q = (uv - 0.5) * _Size.xy;
                    float paper = 0.96 + 0.04 * Noise3(float3(uv * float2(220, 60), 1));
                    albedo = _Tint.rgb * paper;
                    float stripe = step(0.8, uv.y);
                    float thin = smoothstep(0.012, 0.0, abs(uv.y - 0.72)) * 0.8;
                    albedo = lerp(albedo, _Accent.rgb, max(stripe, thin));
                    float lines = smoothstep(0.03, 0.0, abs(frac(uv.y * 3.4 + 0.15) - 0.5) - 0.47) * step(uv.y, 0.66) * 0.15;
                    albedo = lerp(albedo, _Accent.rgb * 0.6, lines);
                    float note = Note(q - float2(_Size.x * 0.5 - _Size.y * 0.42, -_Size.y * 0.1), _Size.y * 0.42) * step(0.5, _Mark);
                    albedo = lerp(albedo, _Accent.rgb * 0.75, note);
                    gloss = 12;
                    shine = 0.12;
                }
                else if (style > 3.5 && style < 4.5)
                {
                    // Turntable: a rubber mat with fine rings on top, brushed metal round its edge.
                    float r = length(p.xz);
                    float top = smoothstep(0.6, 0.8, no.y);
                    float rings = 0.88 + 0.12 * cos(r * 520);
                    float lip = smoothstep(_Size.x * 0.5 - 0.016, _Size.x * 0.5 - 0.012, r);
                    float3 mat = albedo * rings;
                    float3 metal = float3(0.62, 0.62, 0.66) * (0.85 + 0.15 * Noise3(float3(atan2(p.z, p.x) * 60, p.y * 900, 0)));
                    albedo = lerp(metal, lerp(mat, metal * 0.9, lip), top);
                    gloss = lerp(70, 18, top * (1 - lip));
                    shine = lerp(0.7, 0.15, top * (1 - lip));
                }
                else if (style > 4.5 && style < 5.5)
                {
                    // Indicator light: dim glass when off, bright in its glow colour when on.
                    albedo = albedo * 0.35;
                    emit += _GlowColor.rgb * _Glow * 2.4;
                    gloss = 120;
                    shine = 1.0;
                }
                else if (style > 5.5 && style < 6.5)
                {
                    // Brushed metal: fine streaks along X.
                    float streak = Noise3(float3(p.x * 30, p.y * 1400, p.z * 1400));
                    albedo *= 0.82 + 0.3 * streak;
                }
                else if (style > 6.5 && style < 7.5)
                {
                    // Key: plastic with a symbol on its top face that lights up with _Mark.
                    float top = smoothstep(0.55, 0.8, no.y);
                    float2 k = p.xz / max(_Size.xz * 0.5, 1e-4);
                    float s = Symbol(k * 1.25, _Icon, 0.06) * top * step(0.5, _Icon);
                    float3 ink = lerp(float3(0.92, 0.92, 0.95), _GlowColor.rgb, saturate(_Mark));
                    albedo = lerp(albedo, ink, s);
                    emit += _GlowColor.rgb * s * (0.15 + _Mark * 1.4);
                }
                else if (style > 7.5)
                {
                    // Reel hub: a white wheel with six teeth round a dark spindle hole.
                    float r = length(p.xy) / max(_Size.x * 0.5, 1e-4);
                    float a = atan2(p.y, p.x);
                    float teeth = step(0.5, frac(a * 6 / 6.2831853 + 0.25)) * step(r, 0.42) * step(0.26, r);
                    float hole = step(r, 0.3);
                    albedo = lerp(albedo, albedo * 0.25, saturate(teeth + hole * 0.8));
                }

                // Light: a soft key light from above and the viewer's side, a sky-and-floor fill, a gloss highlight
                // and a faint rim against the room.
                float3 l = normalize(float3(0, 1, 0) + v * 0.7);
                float ndl = dot(n, l);
                float diffuse = saturate(ndl * 0.65 + 0.35);
                float hemi = lerp(0.62, 1.0, n.y * 0.5 + 0.5);
                float ndv = saturate(dot(n, v));
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), gloss) * shine;
                float rim = pow(1 - ndv, 3);
                float3 color = albedo * (0.28 + 0.8 * diffuse) * hemi + spec + rim * 0.12 * (albedo + 0.25);
                color += emit;
                // The hover glow: a coloured light round the part's edges and a lift of its whole face. The light and
                // the speakers use their glow in their own way above.
                bool ownGlow = (style > 4.5 && style < 5.5) || (style > 1.5 && style < 2.5);
                color += ownGlow ? 0 : _GlowColor.rgb * _Glow * (0.18 + 1.4 * rim);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
