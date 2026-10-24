// The Heartbeat orb's shell: a cloud of glowing dots spread evenly over a sphere (a Fibonacci lattice), each
// pushed out onto a 3D supershape that morphs with the music. Gielis' superformula gives the shape its petals:
// m sets how many, n1 how sharp they are (the louder the song, the spikier), n2 their outline. Between whole
// petal counts the shape blends from one to the next, so it can grow a petal on the beat without a seam.
// Each band adds its own slow wave over the surface, and every kick sends a ripple from pole to pole.
// Dots on peaks glow gold, dots in the valleys blue, and dots on the far side are dimmer, which gives depth.
// HeartbeatOrb builds the mesh (four corners per dot, all at the dot's direction) and sets the music values.
Shader "MusicInYourHands/OrbDots"
{
    Properties
    {
        _Valley ("Valley colour", Color) = (0.36, 0.48, 1, 1)
        _Middle ("Middle colour", Color) = (1, 0.37, 0.68, 1)
        _Peak ("Peak colour", Color) = (1, 0.83, 0.42, 1)
        _DotSize ("Dot size (share of the orb)", Float) = 0.016
        _Morph ("Petals (set by HeartbeatOrb)", Float) = 4
        _Energy ("Energy 0-1 (set by HeartbeatOrb)", Float) = 0.3
        _Bands ("Bands 1-4 (set by HeartbeatOrb)", Vector) = (0.3, 0.3, 0.3, 0.3)
        _Wave ("Seconds since the last kick (set by HeartbeatOrb)", Float) = 9
        _Kick ("Beat 0-1 (set by HeartbeatOrb)", Float) = 0
        _Clock ("Time (set by HeartbeatOrb)", Float) = 0
        _Alpha ("Fade (set by HeartbeatOrb)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+4" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _Valley;
                float4 _Middle;
                float4 _Peak;
                float _DotSize;
                float _Morph;
                float _Energy;
                float4 _Bands;
                float _Wave;
                float _Kick;
                float _Clock;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 seed : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // The superformula, r(angle) = (|cos(m angle / 4)|^n2 + |sin(m angle / 4)|^n2)^(-1 / n1), divided by its
            // highest value so every petal count gives the same size. Also returns how far up a petal (0-1) this is.
            float Petal(float angle, float m, float n1, float n2, out float peak)
            {
                float c = pow(abs(cos(m * angle * 0.25)), n2);
                float s = pow(abs(sin(m * angle * 0.25)), n2);
                float r = pow(max(c + s, 1e-4), -1.0 / n1);
                float mid = pow(2.0 * pow(0.70710678, n2), -1.0 / n1);
                float lo = min(1.0, mid), hi = max(1.0, mid);
                peak = saturate((r - lo) / max(hi - lo, 1e-4));
                return r / hi;
            }

            // A point on the 3D supershape (two superformula curves, one around and one from pole to pole, with
            // the up axis as the pole) for a whole number of petals.
            float3 Supershape(float lon, float lat, float m, float n1, float n2, out float peak)
            {
                float p1, p2;
                float r1 = Petal(lon, m, n1, n2, p1);
                float r2 = Petal(lat, m, n1 + 0.5, n2, p2);
                peak = p1 * lerp(0.5, 1.0, p2);
                return float3(r1 * cos(lon) * r2 * cos(lat), r2 * sin(lat), r1 * sin(lon) * r2 * cos(lat));
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 dir = normalize(input.positionOS.xyz);
                float t = _Clock;
                float lon = atan2(dir.z, dir.x) + t * 0.25;
                float lat = asin(clamp(dir.y, -1, 1));

                // Petals: blend between the shapes for the two nearest whole petal counts.
                float n1 = lerp(1.0, 0.3, _Energy) + 0.08 * sin(t * 0.8);
                float n2 = 1.4 + 0.4 * sin(t * 0.37);
                float m0 = floor(_Morph);
                float blend = smoothstep(0.0, 1.0, _Morph - m0);
                float peak0, peak1;
                float3 shape0 = Supershape(lon, lat, m0, n1, n2, peak0);
                float3 shape1 = Supershape(lon, lat, m0 + 1.0, n1, n2, peak1);
                float3 shape = lerp(shape0, shape1, blend);
                float peak = lerp(peak0, peak1, blend);
                // Undo the turn in longitude, so the petals spin around the pole.
                float turn = t * 0.25;
                shape.xz = float2(shape.x * cos(turn) + shape.z * sin(turn), -shape.x * sin(turn) + shape.z * cos(turn));
                float radius = 1.0;

                // Each band rolls its own slow wave over the surface.
                radius += _Bands.x * 0.07 * sin(2.0 * lon - t * 1.3) * cos(lat);
                radius += _Bands.y * 0.05 * sin(3.0 * lat + t * 1.9);
                radius += _Bands.z * 0.04 * sin(5.0 * lon + 3.0 * lat - t * 2.6);
                radius += _Bands.w * 0.03 * sin(9.0 * lon - t * 3.4) * cos(lat);

                // The kick ripple: a ring that runs from the top pole to the bottom and fades.
                float fromPole = acos(clamp(dir.y, -1, 1));
                float front = _Wave * 5.0;
                float ripple = exp(-pow((fromPole - front) / 0.32, 2.0)) * exp(-_Wave * 1.6);
                radius += ripple * 0.08 + _Kick * 0.04;

                float3 positionOS = shape * radius * 0.5;
                float3 positionWS = TransformObjectToWorld(positionOS);
                float scale = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));

                // Dots twinkle on their own, swell on the peaks and flare as the ripple passes.
                float twinkle = 0.5 + 0.5 * sin(t * (2.0 + input.seed.x * 3.0) + input.seed.x * 40.0);
                float size = _DotSize * scale * (0.5 + peak * 0.7 + twinkle * 0.3 + ripple * 0.6 + _Kick * 0.2);

                float3 positionVS = TransformWorldToView(positionWS);
                positionVS.xy += (input.uv * 2.0 - 1.0) * size;
                output.positionCS = TransformWViewToHClip(positionVS);
                output.uv = input.uv;

                float tone = peak * peak;
                float3 color = lerp(_Valley.rgb, _Middle.rgb, saturate(tone * 2.2));
                color = lerp(color, _Peak.rgb, saturate(tone * 2.2 - 1.2));
                color = lerp(color, float3(1, 1, 1), saturate(ripple * 0.45));

                float3 normalWS = TransformObjectToWorldDir(dir);
                float facing = dot(normalWS, normalize(GetCameraPositionWS() - positionWS));
                float alpha = lerp(0.22, 1.0, saturate(facing * 0.6 + 0.5)) * (0.45 + twinkle * 0.3 + peak * 0.35 + ripple * 0.5);
                output.color = float4(color, saturate(alpha) * _Alpha);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float d = length(input.uv * 2.0 - 1.0);
                float core = saturate(1.0 - d);
                float glow = core * core * core;
                float3 color = lerp(input.color.rgb, 1, pow(core, 10) * 0.6);
                return half4(color, saturate(glow * 1.6 * input.color.a));
            }
            ENDHLSL
        }
    }
}
