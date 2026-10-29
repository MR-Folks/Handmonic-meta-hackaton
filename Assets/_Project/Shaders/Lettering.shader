// Lyric lettering for TextMesh text: samples the font's glyph texture and adds the lyric worlds' looks.
// - _Frame crops the text softly to a rectangle in a "frame" space (_FrameMatrix: world to frame), so giant
//   letters can run off the edge like a cropped poster (Orbit type).
// - _Spray turns the glyphs into spray paint: a speckled overspray halo and a slightly rough edge (Graffiti).
// - _Outline draws a solid outline in _OutlineColor around the letters, as graffiti pieces have.
// - _Halo adds a soft glow around the letters in their own colour, for neon tubes (Neon sign).
// - _Reveal sprays the word on from left to right, across the text's object-space width (_Extent: min x, max x).
Shader "MusicInYourHands/Lettering"
{
    Properties
    {
        _MainTex ("Font texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Glow ("Extra brightness", Float) = 0
        _Spray ("Spray paint 0-1", Float) = 0
        _Reveal ("Reveal 0-1", Float) = 1
        _Extent ("Text extent in object space (min x, max x)", Vector) = (-1, 1, 0, 0)
        _Frame ("Crop frame (half width, half height, softness; 0 = none)", Vector) = (0, 0, 0.05, 0)
        _Halo ("Glow around the letters 0-1", Float) = 0
        _Outline ("Outline 0-1", Float) = 0
        _OutlineColor ("Outline colour", Color) = (1, 1, 1, 1)
        _Alpha ("Fade", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                float4 _Color;
                float _Glow;
                float _Spray;
                float _Reveal;
                float4 _Extent;
                float4 _Frame;
                float4x4 _FrameMatrix;
                float _Halo;
                float _Outline;
                float4 _OutlineColor;
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
                float3 positionWS : TEXCOORD1;
                float objectX : TEXCOORD2;
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
                output.color = input.color;
                output.objectX = input.positionOS.x;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                float ink = smoothstep(0.3, 0.6, a);
                float3 color = _Color.rgb * input.color.rgb;
                float alpha = ink;

                if (_Spray > 0.001)
                {
                    // Overspray: a soft halo from nearby glyph texels, broken into fine speckles.
                    float2 t = _MainTex_TexelSize.xy * 2.5;
                    float halo = 0;
                    halo += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(t.x, 0)).a;
                    halo += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(t.x, 0)).a;
                    halo += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, t.y)).a;
                    halo += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, t.y)).a;
                    halo += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + t * 0.7).a;
                    halo += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - t * 0.7).a;
                    halo = saturate(halo / 6 * 1.6);
                    float grain = Hash(floor(input.positionWS.xy * 900 + input.positionWS.z * 700));
                    float speckle = step(0.62, grain) * halo * (1 - ink);
                    // A slightly rough paint edge.
                    ink *= step(0.12, grain * 0.4 + ink * 0.9);
                    alpha = lerp(alpha, max(ink, speckle * 0.75), _Spray);
                }

                if (_Outline > 0.001)
                {
                    // Grow the letters by a few texels in eight directions; the rim around the ink is the outline.
                    float2 o = _MainTex_TexelSize.xy * 3.5;
                    float grown = a;
                    grown = max(grown, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(o.x, 0)).a);
                    grown = max(grown, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(o.x, 0)).a);
                    grown = max(grown, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, o.y)).a);
                    grown = max(grown, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, o.y)).a);
                    grown = max(grown, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + o * 0.7).a);
                    grown = max(grown, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - o * 0.7).a);
                    grown = max(grown, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(o.x, -o.y) * 0.7).a);
                    grown = max(grown, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-o.x, o.y) * 0.7).a);
                    float rim = saturate(smoothstep(0.3, 0.6, grown) - ink) * _Outline;
                    color = lerp(color, _OutlineColor.rgb, rim);
                    alpha = max(alpha, rim);
                }

                if (_Halo > 0.001)
                {
                    // A soft glow: the glyph's coverage averaged over two rings of samples around this point.
                    float glow = 0;
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        float a = k * 0.785398;
                        float2 d = float2(cos(a), sin(a)) * _MainTex_TexelSize.xy;
                        glow += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + d * 4).a;
                        glow += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + d * 9).a * 0.6;
                    }
                    glow = saturate(glow / 12.8) * _Halo;
                    // The tube itself burns near-white in its middle.
                    color = lerp(color, 1, ink * 0.45 * _Halo);
                    alpha = max(alpha, glow * 0.85);
                }

                // Spray on from left to right.
                float span = max(_Extent.y - _Extent.x, 1e-4);
                float along = (input.objectX - _Extent.x) / span;
                // Fully revealed words show whole, however wide (_Extent is only set by the worlds that spray words on).
                if (_Reveal < 0.999) alpha *= smoothstep(_Reveal + 0.02, _Reveal - 0.06, along);

                // Soft crop to the frame, like a poster's edge.
                if (_Frame.x > 0)
                {
                    float3 f = mul(_FrameMatrix, float4(input.positionWS, 1)).xyz;
                    float2 inside = _Frame.xy - abs(f.xy);
                    alpha *= saturate(min(inside.x, inside.y) / max(_Frame.z, 1e-4));
                }

                color *= 1 + _Glow;
                return half4(color, saturate(alpha * _Color.a * input.color.a * _Alpha));
            }
            ENDHLSL
        }
    }
}
