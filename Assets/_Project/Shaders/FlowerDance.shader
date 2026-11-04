// The Flower dance garden: every flower is in one mesh, each vertex knowing its flower (uv0.w), its part (uv0.z) and
// its shape at rest in the flower's own space (root at the origin, +Z towards the player). FlowerDance sets each
// flower's pose every frame in four arrays and this shader bends it into place:
// - the stem curves smoothly from its upright root to wherever the dance (or a hand) puts its tip;
// - the leaves ride the stem where they grow and lift about their hinges like arms;
// - the head sits on the stem's tip, squashing and stretching, nodding, and opening its petals: each head vertex
//   holds a closed place (POSITION, NORMAL) and an open one (uv1, uv2.xyz), and the pose blends between them;
// - a tuft of grass round the root sways a little with the flower.
// Parts: 0 stem, 1 leaf, 2 petal, 3 flower centre, 4 calyx (the green cup under the head), 5 grass, 6 shadow.
// Pass 1 lays a soft contact shadow under each flower (it darkens the real floor over passthrough); pass 2 draws the
// flowers solid, in soft two-tone cartoon shading with a light rim, richer and glowing as a flower gets bold.
Shader "MusicInYourHands/FlowerDance"
{
    Properties
    {
        _Shade ("Shadow side brightness", Range(0, 1)) = 0.62
        _Rim ("Rim light", Float) = 0.3
        _ShadowColor ("Contact shadow (alpha: strength)", Color) = (0.02, 0.04, 0.01, 0.42)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-12" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // As many flowers as FlowerDance.MaxFlowers.
        #define FLOWERS 48

        CBUFFER_START(UnityPerMaterial)
            float _Shade;
            float _Rim;
            float4 _ShadowColor;
        CBUFFER_END

        // Per flower, set by FlowerDance every frame through its property block. These live outside UnityPerMaterial,
        // as CubeField's blocks do: declared inside it, the flowers drew in the editor but not on Quest.
        float4 _Root[FLOWERS];  // xyz: where it grows (room space); w: which way it faces (radians about up)
        float4 _Pose[FLOWERS];  // xy: where its tip leans (share of its height, own x and z); z: height scale; w: head scale
        float4 _Look[FLOWERS];  // x: petals open 0-1; y: bold 0-1; z: head squash (- squashed, + stretched); w: head nod (radians)
        float4 _Leaf[FLOWERS];  // x, y: lift of its left and right leaf (radians); z: size (0 until it grows); w: unused

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float4 uv0 : TEXCOORD0;
            float4 uv1 : TEXCOORD1;
            float4 uv2 : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Turns x the way straight up turns into the unit direction t (which never points down).
        float3 UpTo(float3 t, float3 x)
        {
            float3 v = float3(t.z, 0, -t.x);
            float3 vx = cross(v, x);
            return x + vx + cross(v, vx) / (1 + t.y);
        }

        // Turns x about the unit axis k by angle a (radians).
        float3 Turn(float3 x, float3 k, float a)
        {
            float s, c;
            sincos(a, s, c);
            return x * c + cross(k, x) * s + k * dot(k, x) * (1 - c);
        }

        // From a flower's own space (+Z towards the player) to the room's directions.
        float3 Yaw(float3 p, float yaw)
        {
            float s, c;
            sincos(yaw, s, c);
            return float3(p.x * c + p.z * s, p.y, -p.x * s + p.z * c);
        }

        // The stem's centre line at s (0 root, 1 tip) and its direction there: a curve that leaves the ground upright
        // and bends towards the tip, which leans by b (share of the height) on a stem h metres long.
        void Stem(float s, float2 b, float h, out float3 p, out float3 t)
        {
            float lean = length(b);
            if (lean > 0.85) b *= 0.85 / lean;
            float3 p1 = float3(0, 0.5 * h, 0);
            float3 p2 = float3(b.x, sqrt(max(1 - dot(b, b), 0.2)), b.y) * h;
            p = 2 * (1 - s) * s * p1 + s * s * p2;
            t = normalize(2 * (1 - s) * p1 + 2 * s * (p2 - p1) + float3(0, 1e-4, 0));
        }

        // A vertex with nothing to draw in this pass: every corner of its triangles lands on the same point.
        float4 Nowhere() { return float4(0, 0, -2, 1); }
        ENDHLSL

        Pass
        {
            Name "Shadows"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 spot : TEXCOORD0;    // xy: across the shadow (-1..1); z: strength
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                int id = (int)(input.uv0.w + 0.5);
                float size = _Leaf[id].z;
                if (input.uv0.z < 5.5 || size < 1e-3)
                {
                    output.positionCS = Nowhere();
                    return output;
                }
                float4 root = _Root[id];
                float4 pose = _Pose[id];
                float3 tip, along;
                Stem(1, pose.xy, input.uv2.w * pose.z * size, tip, along);
                // Under the flower, between its root and its head, and wider as its head pops.
                float radius = input.uv1.x * size * (0.6 + 0.4 * pose.w);
                float3 centre = root.xyz + Yaw(float3(tip.x, 0, tip.z) * 0.5, root.w);
                float3 positionWS = centre + float3(input.positionOS.x * radius, 0.004, input.positionOS.z * radius);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.spot = float3(input.positionOS.xz, saturate(size * 1.6));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float d = length(input.spot.xy);
                float a = smoothstep(1.0, 0.15, d);
                return half4(_ShadowColor.rgb, a * a * _ShadowColor.a * input.spot.z);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Flowers"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;          // rgb: the part's colour; a: how bold the flower is
                float4 surface : TEXCOORD2;    // x: along the part; y: across it; z: part
                float3 light : TEXCOORD3;      // where the light comes from: above, and the player's side
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                int id = (int)(input.uv0.w + 0.5);
                float part = input.uv0.z;
                float4 leaf = _Leaf[id];
                float size = leaf.z;
                if (part > 5.5 || size < 1e-3)
                {
                    output.positionCS = Nowhere();
                    return output;
                }
                float4 root = _Root[id];
                float4 pose = _Pose[id];
                float4 look = _Look[id];
                float rest = input.uv2.w;
                float h = rest * pose.z * size;

                float3 p, n;
                if (part < 0.5)
                {
                    // Stem: each ring sits round the curve at its height, turned along it.
                    float3 c, t;
                    Stem(input.uv2.x, pose.xy, h, c, t);
                    p = c + UpTo(t, float3(input.positionOS.x, 0, input.positionOS.z) * size);
                    n = UpTo(t, input.normalOS);
                }
                else if (part < 1.5)
                {
                    // Leaf: rides the stem where it grows and lifts about its hinge like an arm.
                    float s = input.uv2.x;
                    float3 c, t;
                    Stem(s, pose.xy, h, c, t);
                    float lift = input.uv1.w > 0.5 ? leaf.y : leaf.x;
                    float3 offset = (input.positionOS.xyz - float3(0, s * rest, 0)) * size;
                    p = c + UpTo(t, Turn(offset, input.uv1.xyz, lift));
                    n = UpTo(t, Turn(input.normalOS, input.uv1.xyz, lift));
                }
                else if (part < 4.5)
                {
                    // Head: closed to open, squashed or stretched along the stem, nodding, on the stem's tip. The
                    // head follows the tip's direction a little more than the stem does, so it whips round.
                    float3 tip, t;
                    Stem(1, pose.xy, h, tip, t);
                    float3 q = lerp(input.positionOS.xyz, input.uv1.xyz, look.x);
                    float3 m = lerp(input.normalOS, input.uv2.xyz, look.x);
                    float2 squash = float2(1 - look.z * 0.5, 1 + look.z);
                    q = float3(q.x * squash.x, q.y * squash.y, q.z * squash.x) * (pose.w * size);
                    m = float3(m.x / squash.x, m.y / squash.y, m.z / squash.x);
                    q = Turn(q, float3(1, 0, 0), look.w);
                    m = Turn(m, float3(1, 0, 0), look.w);
                    float3 th = lerp(float3(0, 1, 0), t, 1.3);
                    th = normalize(float3(th.x, max(th.y, 0.1), th.z));
                    p = tip + UpTo(th, q);
                    n = UpTo(th, m);
                }
                else
                {
                    // Grass round the root, its tips swaying a little with the flower.
                    p = input.positionOS.xyz * size;
                    float bend = p.y * p.y * 8;
                    p.xz += pose.xy * bend;
                    n = input.normalOS;
                }

                float3 positionWS = root.xyz + Yaw(p, root.w);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = Yaw(n, root.w);
                output.color = float4(input.color.rgb, look.y);
                output.surface = float4(input.uv0.xyz, 0);
                output.light = Yaw(float3(0.3, 1.0, 0.7), root.w);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float part = input.surface.z;
                float u = input.surface.x;
                float v = input.surface.y;
                float3 toEye = normalize(GetCameraPositionWS() - input.positionWS);
                float3 n = normalize(input.normalWS);
                // Petals, leaves and grass are thin: light the side that is seen.
                float back = 0;
                bool thin = (part > 0.5 && part < 2.5) || part > 4.5;
                if (thin && dot(n, toEye) < 0)
                {
                    n = -n;
                    back = 1;
                }

                float3 base = input.color.rgb;
                float bold = input.color.a;
                float ao = 1;
                if (part < 0.5)
                {
                    // The stem darkens towards the ground.
                    ao = lerp(0.62, 1.0, saturate(u * 2.5));
                }
                else if (part < 1.5)
                {
                    // Leaves: darker where they leave the stem, a lighter vein down the middle.
                    base *= lerp(0.8, 1.06, u);
                    base = lerp(base, base * 1.35 + 0.015, smoothstep(0.2, 0.0, abs(v)) * (1 - u * 0.6) * 0.75);
                }
                else if (part < 2.5)
                {
                    // Petals: deeper at the heart of the flower, with a faint crease down the middle.
                    ao = lerp(0.66, 1.0, smoothstep(0.0, 0.5, u));
                    base *= lerp(1.0, 0.86, smoothstep(0.25, 0.0, abs(v)) * (1 - u) * 0.6);
                }
                else if (part < 3.5)
                {
                    // The flower's centre: a soft grid of seeds, lighter in the middle.
                    float2 g = input.surface.xy * 4.5;
                    float seed = smoothstep(0.38, 0.18, length(frac(g) - 0.5));
                    base *= lerp(1.0, 0.7, seed) * lerp(1.15, 0.9, saturate(length(input.surface.xy)));
                }
                else if (part > 4.5)
                {
                    // Grass: dark at the roots, fresh at the tips.
                    base *= lerp(0.6, 1.1, u);
                }

                float ndl = dot(n, normalize(input.light));
                float lit = lerp(_Shade, 1.0, smoothstep(-0.1, 0.35, ndl)) * lerp(1.0, 0.86, back);
                float3 color = base * lit * ao;
                float rim = pow(1 - saturate(dot(n, toEye)), 3);
                color += rim * _Rim * lerp(float3(1, 1, 1), base, 0.5);

                // Bold: richer, brighter colour that glows from inside, strongest in the head.
                float head = part > 1.5 && part < 3.5 ? 1.0 : 0.25;
                float grey = dot(color, float3(0.3, 0.59, 0.11));
                float3 rich = max(0, lerp(grey, color, 1.6)) * 1.3 + base * 0.3 + rim * base * 1.5;
                color = lerp(color, rich, saturate(bold * head));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
