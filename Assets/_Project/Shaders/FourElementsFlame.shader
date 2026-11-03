// Four elements: a living flame on a camera-facing quad (uv.y = 0 at its base, 1 at its tip). A teardrop of fire whose
// edges are torn by turbulence that scrolls upwards, white-gold at its heart and deep red at its ragged edges, its tip
// leaning with _Bend (the hand's movement). With _Shape 1 it is a fireball instead: a ball of flame whose fire streams
// back along _Bend (the way it came from). Additive, and it writes alpha so it shows over passthrough.
Shader "MusicInYourHands/FourElementsFlame"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 1
        _Shape ("Shape (0 flame, 1 fireball)", Float) = 0
        _Bend ("Lean of the tip, or a fireball's trail, in quad space (set by FourElements)", Vector) = (0, 0, 0, 0)
        _Flare ("Beat flare 0-1 (set by FourElements)", Float) = 0
        _Seed ("Seed (set by FourElements)", Float) = 0
        _Clock ("Time (set by FourElements)", Float) = 0
        _Alpha ("Fade (set by FourElements)", Float) = 1
        _DepthPull ("How far towards the eye it counts as, for what hides it (m)", Float) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+12" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float _Intensity;
                float _Shape;
                float4 _Bend;
                float _Flare;
                float _Seed;
                float _Clock;
                float _Alpha;
                float _DepthPull;
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
                // Pulled a few centimetres towards the eye along its own line of sight: it lands on the same spot on
                // screen, but fingers curled just in front of it no longer hide the flame on the palm.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 toEye = GetCameraPositionWS() - positionWS;
                float range = length(toEye);
                positionWS += toEye / max(range, 1e-4) * min(_DepthPull, range * 0.5);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            // A hash that stays even for large coordinates (the turbulence scrolls on for as long as the song plays).
            float Hash(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // Four octaves of noise, each turned against the last, so no grid lines show in the fire.
            float Fbm(float2 p)
            {
                const float2x2 turn = float2x2(0.8, -0.6, 0.6, 0.8);
                float v = 0, a = 0.5;
                p = mul(turn, p);
                [unroll] for (int i = 0; i < 4; i++)
                {
                    v += Noise(p) * a;
                    p = mul(turn, p) * 2.07 + float2(17.1, 3.7);
                    a *= 0.5;
                }
                return v / 0.9375;
            }

            // Colour of fire by heat: deep red, orange, gold, then a pale white-gold heart.
            float3 Fire(float heat)
            {
                float3 c = lerp(float3(0.55, 0.05, 0.02), float3(1.0, 0.36, 0.05), smoothstep(0.05, 0.4, heat));
                c = lerp(c, float3(1.0, 0.7, 0.18), smoothstep(0.38, 0.68, heat));
                return lerp(c, float3(1.0, 0.95, 0.78), smoothstep(0.72, 1.0, heat));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _Clock;
                float heat, halo;
                if (_Shape < 0.5)
                {
                    float x = input.uv.x * 2 - 1;
                    float y = input.uv.y;
                    // Everything fades out before the quad's edges, so they never show.
                    float edges = smoothstep(1.0, 0.75, abs(x)) * smoothstep(0.0, 0.12, y);
                    // The tip leans with the hand's movement; the base stays on the palm.
                    x -= _Bend.x * y * y;
                    float n1 = Fbm(float2(x * 1.5 + _Seed, y * 2.0 - t * 2.4));
                    float n2 = Fbm(float2(x * 3.2 - _Seed * 1.3, y * 4.2 - t * 3.9));
                    // Sways more towards the tip, like a real flame.
                    x += (n1 - 0.5) * 0.55 * y;
                    // A teardrop: a round base, widest a quarter of the way up, tapering to a point.
                    float width = 0.8 * min(sqrt(saturate(y / 0.22)), pow(saturate((1 - y) / 0.78), 0.9)) + 0.01;
                    float r = abs(x) / width;
                    float body = 1 - r * r;
                    // Turbulence tears the edges, more raggedly near the top; the heart sits a little above the base,
                    // which burns cooler.
                    heat = body * (0.98 - 0.4 * y) * lerp(0.55, 1.0, smoothstep(0.0, 0.2, y)) + (n2 - 0.5) * (0.45 + 0.8 * y) + (n1 - 0.5) * 0.3;
                    // Licks of flame breaking off above the tip.
                    float tongue = saturate((n2 - 0.55) * 3.0) * smoothstep(0.4, 0.8, y) * saturate(1 - abs(x) * 1.4) * (1 - y);
                    heat = max(heat, tongue * 0.8);
                    heat *= smoothstep(0.0, 0.04, y) * smoothstep(1.0, 0.86, y) * edges;
                    // A soft glow of heat around the flame.
                    halo = saturate(1 - r * 0.55) * saturate(1 - y * 1.1) * 0.3 * edges;
                }
                else
                {
                    float2 p = input.uv * 2 - 1;
                    float2 back = _Bend.xy;
                    float along = dot(p, back);
                    // Stretched back along the way it came, so the fire trails behind it.
                    float2 q = p - back * max(along, 0) * 0.65;
                    float r = length(q);
                    float2 outward = p / max(length(p), 1e-3);
                    float n1 = Fbm(p * 1.9 - outward * t * 1.8 + _Seed);
                    float n2 = Fbm(p * 3.8 + back * t * 4.0 - _Seed);
                    heat = (0.6 - r) * 2.1 + (n1 - 0.5) * 0.7 + (n2 - 0.5) * 0.5 * saturate(0.4 + along);
                    // A white-hot core.
                    heat = max(heat, saturate(1 - length(p) * 2.6) * 1.05);
                    halo = saturate(1 - r * 1.1) * 0.35;
                }
                heat = saturate(heat);
                float body = smoothstep(0.02, 0.3, heat) * _Intensity * (1 + _Flare * 0.3);
                halo *= 1 + _Flare;
                float alpha = saturate(body + halo);
                // Additive, so the colour is weighted by each part's share of the light.
                float3 color = (Fire(heat) * body + float3(1.0, 0.32, 0.06) * halo) / max(body + halo, 1e-3);
                return half4(color * (1 + _Flare * 0.4), alpha * _Alpha);
            }
            ENDHLSL
        }
    }
}
