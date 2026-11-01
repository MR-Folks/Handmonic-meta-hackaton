// The Waveform ribbon's silk: a see-through band of light whose inside slowly changes between three looks: soft folds,
// fine threads running along it, and a woven lattice. Colour runs from one hand's colour to the other's (WaveformRibbon
// moves them through its palette over time), and its edges fade out so it reads as fabric, not a plank. Patterns are
// measured in metres along the band (_Length), so they keep their size as the hands move, and fine ones smooth out when
// too small to see instead of flickering.
// The mesh's uv.x runs along the band (0-1) and uv.y across it; vertex alpha fades the ends and old trails.
Shader "MusicInYourHands/Silk"
{
    Properties
    {
        _ColorA ("Colour at the left hand (set by WaveformRibbon)", Color) = (1, 0.83, 0.42, 1)
        _ColorB ("Colour at the right hand (set by WaveformRibbon)", Color) = (1, 0.37, 0.68, 1)
        _Length ("Band length in metres (set by WaveformRibbon)", Float) = 0.4
        _Look ("Which look, 0-3, between whole numbers a blend (set by WaveformRibbon)", Float) = 0
        _Kick ("Beat 0-1 (set by WaveformRibbon)", Float) = 0
        _Clock ("Time (set by WaveformRibbon)", Float) = 0
        _Alpha ("Fade (set by WaveformRibbon)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+6" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _ColorA;
                float4 _ColorB;
                float _Length;
                float _Look;
                float _Kick;
                float _Clock;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float Wave(float x) { return 0.5 + 0.5 * sin(x); }

            // Soft folds of cloth, wandering rather than repeating as even stripes.
            float Folds(float x, float v, float t)
            {
                float warp = sin(x * 9 + t * 0.7) * 1.1 + sin(x * 23 - t * 1.1 + v * 4) * 0.4;
                float s = Wave(x * 38 - t * 2.4 + v * 3 + warp) * 0.7 + Wave(x * 71 + t * 1.7 - v * 5 + warp * 0.5) * 0.3;
                return s * s * (3 - 2 * s);
            }

            // Fine threads running along the band, swaying a little, with light travelling down them.
            float Fibres(float x, float v, float t, float fineAcross)
            {
                float lines = pow(Wave(v * 88 + sin(x * 11 + t * 0.9) * 0.9), 3);
                return lerp(0.35, lines, fineAcross) * (0.4 + pow(Wave(x * 34 - t * 4 + v * 6), 4) * 0.8);
            }

            // A woven lattice of soft diamonds, sliding slowly through itself.
            float Lattice(float x, float v, float t, float fineAlong)
            {
                float woven = Wave(x * 70 + v * 20 - t * 1.6) * Wave(x * 70 - v * 20 + t * 1.2);
                return lerp(0.3, woven * 1.6, fineAlong);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float u = input.uv.x, v = input.uv.y;
                float x = u * _Length;
                float t = _Clock;
                float edge = pow(sin(v * PI), 0.8);
                float rim = exp(-pow((abs(v - 0.5) - 0.46) / 0.035, 2));

                // 1 while a pattern has room on screen, falling to 0 as it nears a pixel across.
                float fineAcross = saturate(1.5 - fwidth(v) * 88);
                float fineAlong = saturate(1.5 - fwidth(x) * 70);

                // How much of each look shows: each fades into the next, and the last back into the first.
                float3 shares = saturate(1 - abs(frac((_Look - float3(0, 1, 2)) / 3 + 0.5) - 0.5) * 3);
                float light = shares.x * Folds(x, v, t) + shares.y * Fibres(x, v, t, fineAcross) + shares.z * Lattice(x, v, t, fineAlong);

                float3 color = lerp(lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0, 1, u)), 1, light * 0.45 + rim * 0.3);
                float alpha = (0.14 + light * 0.45 + rim * 0.45 + _Kick * 0.12) * edge;
                return half4(color, saturate(alpha * input.color.a * _Alpha));
            }
            ENDHLSL
        }
    }
}
