// Bubble notes' fish: small swimming fish, all drawn in one instanced draw call. The mesh (built by BubbleNotes) is a fish
// one unit long with its nose at +z and its back up: uv.x runs from the nose (0) to the tip of the tail (1), uv.y names
// the part (0 body, 1 tail, 2 back fin, 3 side fins, 4 belly fin) and uv2 is the place on a fin (from its root out to
// its edge, and across it). The vertex shader bends the body in a wave that runs back to the tail, so each fish swims
// (_Swim: x the stroke's phase, y how wide it is) and its side fins paddle. The fragment shader gives a deep back, the
// fish's colour on its flanks and a pale belly, a soft soap-film sheen like the bubbles', a stripe along each side that
// glows with the fish's part of the music (_Tint.a), dark eyes with a glint, fins that look see-through with fine rays,
// a rim of pale water light, light dancing over the back, and a glow when the beat's wave passes (_Swim.z).
// Real things hide the fish behind them while the real room's depth is on (RoomOcclusion.hlsl): they swim behind your sofa.
Shader "MusicInYourHands/BubbleNotesFish"
{
    Properties
    {
        _Light ("Light direction", Vector) = (0.25, 1, -0.35, 0)
        _Water ("Water light colour", Color) = (0.62, 0.95, 1, 1)
        _EnvironmentDepthBias ("Real-room depth bias (share of the distance)", Float) = 0.03
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
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RoomOcclusion.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Light;
                float4 _Water;
                float _EnvironmentDepthBias;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Fish)
                // rgb the fish's colour (linear), a how brightly its stripes glow.
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
                // x the swimming stroke's phase, y its width, z the beat's glow, w a seed of its own.
                UNITY_DEFINE_INSTANCED_PROP(float4, _Swim)
            UNITY_INSTANCING_BUFFER_END(Fish)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 fin : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                // The point and its normal on the fish at rest, before the swimming bend.
                float3 local : TEXCOORD2;
                float3 localNormal : TEXCOORD3;
                // x along the fish, y the part, zw the place on a fin.
                float4 part : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 swim = UNITY_ACCESS_INSTANCED_PROP(Fish, _Swim);

                float s = input.uv.x;
                float part = input.uv.y;
                float3 p = input.positionOS.xyz;
                float3 n = input.normalOS;

                // The swimming wave: a sideways bend that grows towards the tail and runs back along the body. The
                // head hardly moves. z = 0.5 - s, so the bend's slope along z is minus its slope along s.
                const float k = 4.4;
                float envelope = 0.03 + 0.97 * s * s;
                float wave = swim.x - s * k;
                float bend = swim.y * envelope * sin(wave);
                float slope = swim.y * (1.94 * s * sin(wave) - envelope * k * cos(wave));
                p.x += bend;
                n.z += slope * n.x;

                // The side fins paddle gently, half as fast as the tail beats.
                float side = step(2.5, part) * step(part, 3.5);
                p.y += side * sin(swim.x * 0.5 + 1.3) * 0.022 * input.fin.x;

                output.positionWS = TransformObjectToWorld(p);
                output.normalWS = TransformObjectToWorldNormal(normalize(n));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.local = input.positionOS.xyz;
                output.localNormal = input.normalOS;
                output.part = float4(s, part, input.fin);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Fish, _Tint);
                float4 swim = UNITY_ACCESS_INSTANCED_PROP(Fish, _Swim);
                float3 colour = tint.rgb;

                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 l = normalize(_Light.xyz);
                float s = input.part.x;
                float onFin = step(0.5, input.part.y);
                float3 q = input.local;
                float3 m = normalize(input.localNormal);

                float wrap = saturate(dot(n, l) * 0.5 + 0.5);
                float facing = saturate(dot(n, v));
                float rim = pow(1 - facing, 3);

                // Body: a deep back, the fish's colour on its flanks and a pale belly, as real fish are shaded.
                float3 back = colour * 0.3 + float3(0.02, 0.04, 0.09);
                float3 belly = lerp(colour, float3(1, 0.97, 0.92), 0.72);
                float3 body = lerp(colour, back, smoothstep(0.2, 0.9, m.y));
                body = lerp(body, belly, smoothstep(-0.25, -0.85, m.y));
                // A soft soap-film sheen, its hues moving over the fish as it turns.
                float3 film = 0.5 + 0.5 * cos(6.2832 * (rim * 0.8 + s * 0.6 + swim.w + float3(0, 0.33, 0.67)));
                body = lerp(body, film, 0.12 + rim * 0.3);
                float3 shade = body * (0.42 + 0.72 * wrap);

                // The fins: lighter and see-through-looking, with fine rays running out to a brighter edge.
                float rays = 0.5 + 0.5 * cos(input.part.w * 26.0);
                float edge = smoothstep(0.55, 1.0, input.part.z);
                float3 finColour = lerp(colour, float3(1, 1, 1), 0.4) * (0.5 + 0.22 * rays + 0.35 * edge);
                float3 c = lerp(shade, finColour * (0.6 + 0.5 * wrap), onFin);

                // Pale water light round the edge, a gloss, and light dancing over the back.
                c += _Water.rgb * rim * lerp(0.6, 0.3, onFin);
                c += pow(saturate(dot(reflect(-l, n), v)), 24) * 0.55 * (1 - onFin * 0.6);
                float2 w = input.positionWS.xz * 24.0 + _Time.y * float2(0.9, 0.7);
                float caustic = saturate(sin(w.x + 1.7 * sin(w.y * 0.8)) * sin(w.y + 1.3 * sin(w.x * 0.9 + _Time.y)));
                c += _Water.rgb * caustic * caustic * saturate(n.y) * 0.22 * (1 - onFin);

                // A stripe along each side, glowing with the fish's part of the music.
                float stripe = exp(-pow((m.y - 0.12) / 0.2, 2)) * smoothstep(0.12, 0.24, s) * smoothstep(0.72, 0.5, s) * (1 - onFin);
                c += lerp(colour, float3(1, 1, 1), 0.3) * stripe * (0.25 + tint.a * 1.1);

                // The beat's wave of light passing through.
                c += (colour * 0.7 + 0.2) * swim.z;

                // Eyes, one each side behind the nose: a golden ring, a dark pupil and a glint.
                float sideways = smoothstep(0.35, 0.7, abs(m.x)) * (1 - onFin);
                float2 e = float2(q.z - 0.405, q.y - 0.02);
                float de = length(e);
                float iris = smoothstep(0.036, 0.03, de) * sideways;
                float pupil = smoothstep(0.024, 0.018, de) * sideways;
                float glint = smoothstep(0.009, 0.004, length(e - float2(0.008, 0.009))) * sideways;
                c = lerp(c, float3(0.85, 0.7, 0.35) * (0.5 + 0.5 * wrap), iris);
                c = lerp(c, float3(0.02, 0.02, 0.03), pupil);
                c += glint * 0.9;
                // Opaque over passthrough: where something real is in front, colour and alpha fade out together.
                half4 result = half4(c, 1);
                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY_WORLDPOS(input.positionWS, result, _EnvironmentDepthBias);
                return result;
            }
            ENDHLSL
        }
    }
}
