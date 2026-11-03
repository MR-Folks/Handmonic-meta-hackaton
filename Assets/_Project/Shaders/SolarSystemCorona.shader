// The Solar system's sunlight: a camera-facing quad just in front of the sun that draws its corona. A hot glow hugs the
// edge of the disc and slow streamers reach out from it, turning as they go; a wide soft light spreads around the whole
// sun; and on each beat a ring of light flares out from its surface (_Flare, _FlareAge). Warming the sun with a palm
// (_Warm) makes the corona reach further and burn hotter. _Disc is the sun's radius as a share of the quad's half size:
// inside it the glow only lifts the edge of the disc, so the boiling surface shows through in the middle.
Shader "MusicInYourHands/SolarSystemCorona"
{
    Properties
    {
        _Color ("Corona colour", Color) = (1, 0.56, 0.18, 1)
        _Hot ("Hot colour", Color) = (1, 0.92, 0.66, 1)
        _Disc ("Sun's radius, share of the quad (set by SolarSystem)", Float) = 0.3
        _Glow ("Brightness (set by SolarSystem)", Float) = 1
        _Flare ("Beat flare 0-1 (set by SolarSystem)", Float) = 0
        _FlareAge ("Seconds since the flare (set by SolarSystem)", Float) = 9
        _Warm ("Warmth 0-1 (set by SolarSystem)", Float) = 0
        _Clock ("Time (set by SolarSystem)", Float) = 0
        _Alpha ("Fade (set by SolarSystem)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+3" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _Hot;
                float _Disc;
                float _Glow;
                float _Flare;
                float _FlareAge;
                float _Warm;
                float _Clock;
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

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv * 2 - 1;
                float r = length(p);
                float rs = max(_Disc, 1e-3);
                // How far out from the edge of the disc, in sun radii.
                float x = max(r - rs, 0) / rs;
                float2 dir = p / max(r, 1e-4);

                // Streamers: noise sampled round a circle (so it has no seam), turning slowly and drifting outwards.
                float t = _Clock;
                float a = t * 0.04;
                float2 turned = float2(dir.x * cos(a) - dir.y * sin(a), dir.x * sin(a) + dir.y * cos(a));
                float streak = Noise(float3(turned * 2.4, t * 0.12 - x * 0.3)) * 0.7
                             + Noise(float3(turned * 5.5, t * 0.25 - x * 0.55 + 4.0)) * 0.3;
                float reach = (0.14 + streak * streak * streak * 2.2) * (1 + _Warm * 1.1 + _Glow * 0.15);
                float corona = exp(-x / reach);
                float light = exp(-x * 0.8 / (1 + _Warm * 0.7)) * 0.3;

                // Inside the disc only a thin rim of light along its edge, meeting the corona there without a seam.
                float inner = smoothstep(rs * 0.8, rs, r);
                float outside = step(rs, r);
                float shell = lerp(inner * inner * inner, corona, outside);
                float wide = lerp(inner * inner * light, light, outside);

                // The beat's flare: a ring of light leaving the surface.
                float front = rs * (1 + _FlareAge * 2.4);
                float ring = exp(-pow((r - front) / (rs * 0.25), 2)) * exp(-_FlareAge * 3.0) * _Flare * outside;

                float edge = smoothstep(1.0, 0.75, r);
                float k = (shell * (0.75 + _Glow * 0.55) + wide * (0.5 + _Glow * 0.7) + ring * 0.9) * edge;
                float3 color = lerp(_Color.rgb, _Hot.rgb, saturate(shell * (0.55 + _Warm * 0.45) + ring * 0.5));
                return half4(color, saturate(k * _Alpha));
            }
            ENDHLSL
        }
    }
}
