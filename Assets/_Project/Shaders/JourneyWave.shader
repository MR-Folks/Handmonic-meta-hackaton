// Journey mode's change of section, on a floor, wall or ceiling quad and on a low curtain that rides the ring outward
// (JourneyWave): a soft ring of light spreading out from the player's feet (_Origin) across the room, _Radius metres out
// by now, _Width wide and _Strength bright. Where the ring meets a wall it climbs it as an arc, then crosses the ceiling;
// the curtain (a cylinder of _Radius round the feet) glows where it meets the floor and fades by waist height. Nothing
// lights within a metre of the player, so nothing comes near the head. The finale (_Bloom) sends a second, fainter ring
// after the first. During the bar before a change, a faint glow gathers on the floor round the feet (_Gather).
// Everything is light added to the room, soft at its edges and fading with distance, so it never reads as a flash.
Shader "MusicInYourHands/JourneyWave"
{
    Properties
    {
        _Color ("Colour of the new world", Color) = (1, 0.85, 0.6, 1)
        _Origin ("Where the wave starts (the feet)", Vector) = (0, 0, 0, 0)
        _Radius ("How far out the ring is (m)", Float) = 1
        _Width ("How wide the ring is (m)", Float) = 0.22
        _Strength ("Brightness 0-1", Float) = 0.4
        _Gather ("The glow gathering at the feet before a change, 0-1", Float) = 0
        _Bloom ("Finale: a second ring follows, 0-1", Float) = 0
        _Size ("Quad size (m)", Vector) = (3, 2.5, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _Origin;
                float _Radius;
                float _Width;
                float _Strength;
                float _Gather;
                float _Bloom;
                float4 _Size;
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

            float Band(float d, float centre, float width)
            {
                float x = (d - centre) / max(width, 1e-3);
                return exp(-x * x);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p = input.positionWS;
                float d = distance(p, _Origin.xyz);

                // The ring, a soft wash just behind it, and the finale's second ring a metre further back.
                float ring = Band(d, _Radius, _Width);
                float wash = saturate((_Radius - d) / max(_Radius, 0.2)) * saturate(d / 0.6) * 0.18;
                float echo = _Bloom * Band(d, _Radius - 1.0, _Width * 1.4) * 0.55 * step(1.0, _Radius);
                // A gentle shimmer along the ring, slow enough never to flicker.
                float shimmer = 0.85 + 0.15 * sin(atan2(p.z - _Origin.z, p.x - _Origin.x) * 9 + _Time.y * 1.3 + d * 4);
                // Kept a metre clear of the player: the ring only lights once it is out past the body.
                float clear = smoothstep(0.6, 1.2, length(p.xz - _Origin.xz));
                float light = (ring * shimmer + wash + echo) * _Strength * saturate(1.15 - d / 7.0) * clear;

                // The glow gathering round the feet, on the floor only.
                float floorish = saturate(1 - abs(p.y - _Origin.y) / 0.3);
                light += _Gather * 0.35 * exp(-d * d / 0.45) * floorish;

                float3 color = lerp(_Color.rgb, 1, ring * 0.35);
                // Fade towards the quad's edges, so the room has no hard frame.
                float2 e = min(input.uv, 1 - input.uv) * _Size.xy;
                float edge = smoothstep(0, 0.25, min(e.x, e.y));
                return half4(color, saturate(light * edge));
            }
            ENDHLSL
        }
    }
}
