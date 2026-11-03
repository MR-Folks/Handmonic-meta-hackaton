// The Solar system's orbits: thin lines of light round the sun. SolarSystem builds a ring of points (each point twice,
// with the line's direction in the normal and the side, 0 or 1, in uv.y; uv.x runs 0-1 round the ring), and the
// vertex shader turns the strip to face the viewer, keeping it _Width metres wide however large the ring is drawn.
// A brighter trail runs behind the planet (_Head: where the planet is, 0-1 round the ring), the far side of the ring
// is dimmer than the near side, _Pulse lights the whole ring (the beat's ripple, a planet just let go), and _Dashes
// breaks it into dashes that flow along it: the orbit a carried planet will take when you let it go.
Shader "MusicInYourHands/SolarSystemOrbit"
{
    Properties
    {
        _Color ("Color", Color) = (0.7, 0.8, 1, 1)
        _Alpha ("Fade (set by SolarSystem)", Float) = 1
        _Width ("Line width, m (set by SolarSystem)", Float) = 0.0012
        _Head ("Where the planet is, 0-1 round the ring (set by SolarSystem)", Float) = 0
        _Trail ("Trail behind the planet (set by SolarSystem)", Float) = 0.6
        _Pulse ("Extra light 0-1 (set by SolarSystem)", Float) = 0
        _Dashes ("Dashes round the ring, 0 for a solid line (set by SolarSystem)", Float) = 0
        _Clock ("Time (set by SolarSystem)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float _Alpha;
                float _Width;
                float _Head;
                float _Trail;
                float _Pulse;
                float _Dashes;
                float _Clock;
            CBUFFER_END

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
                float2 uv : TEXCOORD0;
                float near : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 centreWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 tangentWS = TransformObjectToWorldDir(input.normalOS);
                float3 eye = GetCameraPositionWS();
                float3 side = cross(tangentWS, eye - centreWS);
                side = dot(side, side) > 1e-10 ? normalize(side) : float3(0, 1, 0);
                // The strip is three line-widths to each side, leaving room for the soft glow around the core.
                float3 positionWS = centreWS + side * (_Width * 3 * (input.uv.y * 2 - 1));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                float3 outward = centreWS - originWS;
                float3 toEye = eye - originWS;
                output.near = dot(outward, toEye) * rsqrt(max(dot(outward, outward) * dot(toEye, toEye), 1e-10)) * 0.5 + 0.5;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float across = abs(input.uv.y * 2 - 1) * 3;
                float core = exp(-across * across * 1.4);
                float glow = exp(-across * across * 0.25) * 0.2;
                float behind = frac(_Head - input.uv.x);
                float trail = exp(-behind * 9) * _Trail;
                float dash = 1;
                if (_Dashes > 0.5)
                    dash = smoothstep(0.2, 0.45, abs(frac(input.uv.x * _Dashes - _Clock * 1.5) - 0.5));
                float k = (core + glow) * lerp(0.35, 1.0, input.near) * (1 + trail * 4 + _Pulse * 3) * dash;
                float3 color = lerp(_Color.rgb, float3(1, 1, 1), saturate(trail * 0.5 + _Pulse * 0.35) * core);
                return half4(color, saturate(k * _Color.a * _Alpha));
            }
            ENDHLSL
        }
    }
}
