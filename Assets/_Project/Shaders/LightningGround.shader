// Lightning's ground: the snow settled on the floor and the furniture, and the wet ground and puddles the rain leaves,
// drawn on a quad over each surface Lightning keeps a grid for. The grid comes as a small texture, _Cover, one texel
// per point: r the snow (0-1, a solid white cover at 1), g the water (0-1), b how low the spot is
// (puddles gather in low spots), a the fade at the grid's edge, read with bilinear filtering.
// - Snow: grainy and patchy while thin (the grain decides where a thin layer shows), then a white blanket, bluish in
//   its dips, with a few glints that twinkle.
// - Water: darkens the floor where it is wet; a puddle where it has gathered, dark and glossy, shining towards the
//   horizon and, while it rains (_Rain), ringing now and then with small ripples.
// _Flash lights it all for a moment with the lightning. Alpha blended (dark water shades the room behind), and writes
// alpha so it shows over passthrough.
Shader "MusicInYourHands/LightningGround"
{
    Properties
    {
        _Cover ("Snow, water, low spots, edge", 2D) = "black" {}
        _Flash ("Flash 0-1", Float) = 0
        _Rain ("Raining 0-1", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-30" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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

            TEXTURE2D(_Cover);
            SAMPLER(sampler_Cover);

            CBUFFER_START(UnityPerMaterial)
                float4 _Cover_ST;
                float _Flash;
                float _Rain;
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
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
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

            // A random number 0-1 for each point of a lattice, with no pattern along its rows.
            float Hash(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 Slope(float2 i)
            {
                return float2(Hash(i), Hash(i + 57.3)) * 2 - 1;
            }

            // Smooth gradient noise, 0-1.
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * f * (f * (f * 6 - 15) + 10);
                float a = dot(Slope(i), f);
                float b = dot(Slope(i + float2(1, 0)), f - float2(1, 0));
                float c = dot(Slope(i + float2(0, 1)), f - float2(0, 1));
                float d = dot(Slope(i + 1), f - 1);
                return saturate(0.5 + lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y));
            }

            // Three layers of noise, each turned against the last so no grid shows through.
            float Grain(float2 p)
            {
                const float2x2 turn = float2x2(0.8, -0.6, 0.6, 0.8);
                float2 q = mul(turn, p);
                float2 s = mul(turn, q);
                float sum = Noise(p * 3.7) * 0.45 + Noise(q * 11.3 + 7.3) * 0.33 + Noise(s * 31.1 + 3.9) * 0.22;
                return saturate((sum - 0.5) * 1.7 + 0.5);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 c = SAMPLE_TEXTURE2D(_Cover, sampler_Cover, input.uv);
                if (c.r + c.g < 0.004 || c.a < 0.004) return half4(0, 0, 0, 0);
                float2 xz = input.positionWS.xz;
                float grain = Grain(xz);

                // Snow: thin snow shows only where the grain lets it, so it starts as a patchy dusting.
                float snow = c.r;
                float cover = smoothstep(0.12, 0.5, snow * (0.35 + grain * 1.3));
                float shade = saturate(0.6 + snow * 0.3 + (grain - 0.5) * 0.5);
                float3 snowColour = lerp(float3(0.5, 0.58, 0.78), float3(0.97, 0.98, 1), shade);
                // Glints: a few round sparkles in the snow, each twinkling in its own time.
                if (cover > 0.01)
                {
                    float2 glintAt = xz * 30;
                    float glintCell = Hash(floor(glintAt));
                    float glintDot = saturate(1 - length(frac(glintAt) - 0.5) * 5);
                    snowColour += step(0.97, glintCell) * glintDot * glintDot * pow(saturate(sin(_Time.y * 2.3 + glintCell * 60)), 6) * cover * 1.2;
                }

                // Water: damp floor, and puddles where it has run together in the low spots.
                float water = c.g;
                float puddle = smoothstep(0.5, 0.78, water + c.b * 0.2 + (grain - 0.5) * 0.18);
                float3 toEye = normalize(GetCameraPositionWS() - input.positionWS);
                float grazing = pow(1 - saturate(abs(toEye.y)), 3);
                // Now and then a small ripple rings in a puddle while it rains: a few cells of a fine grid at a time.
                float ripple = 0;
                if (puddle * _Rain > 0.01)
                {
                    float2 cell = xz / 0.16;
                    float2 id = floor(cell);
                    float h = Hash(id);
                    float beat = _Time.y * 0.7 + h;
                    float lit = step(0.7, Hash(id + floor(beat) * 7.31));
                    float2 centre = float2(Hash(id + 17.1), Hash(id + 41.7)) * 0.4 + 0.3;
                    float phase = frac(beat);
                    float d = length(frac(cell) - centre) - phase * 0.3;
                    ripple = exp(-d * d * 1600) * (1 - phase) * lit * puddle * _Rain * 0.7;
                }
                float3 waterColour = float3(0.02, 0.035, 0.06) + float3(0.45, 0.58, 0.85) * (0.1 + grazing * 0.6) * puddle
                                   + float3(0.6, 0.75, 1) * ripple * 0.8;
                float waterAlpha = saturate(water * 1.5) * 0.3 + puddle * 0.35 + ripple * 0.25;

                float3 colour = lerp(waterColour, snowColour, cover);
                float alpha = lerp(waterAlpha, 0.92, cover);
                colour += _Flash * lerp(float3(0.45, 0.55, 0.85) * (0.3 + puddle), float3(0.3, 0.32, 0.35), cover);
                return half4(colour, saturate(alpha * c.a));
            }
            ENDHLSL
        }
    }
}
