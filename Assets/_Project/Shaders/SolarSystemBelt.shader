// The Solar system's asteroid belt: hundreds of small glints between Mars and Jupiter, all in one mesh. Each asteroid
// is a quad whose four corners hold its orbit (radius, height above the plane, starting angle) in the position and its
// seed and size in uv2. The vertex shader moves it round the sun at Kepler's speed for its distance (_Orbit is the
// system's orbit clock, _Omega the angular speed at the _Reference distance) and turns it to face the viewer. Now and
// then an asteroid glints, more often and brighter with the high band (_Glow); the far side of the belt is dimmer.
Shader "MusicInYourHands/SolarSystemBelt"
{
    Properties
    {
        _Color ("Rock colour", Color) = (0.86, 0.78, 0.66, 1)
        _Glint ("Glint colour", Color) = (1, 0.95, 0.82, 1)
        _Size ("Asteroid size (m at full size)", Float) = 0.0026
        _Orbit ("Orbit clock (set by SolarSystem)", Float) = 0
        _Omega ("Angular speed at the reference distance (set by SolarSystem)", Float) = 0.4
        _Reference ("Reference distance (set by SolarSystem)", Float) = 0.16
        _Glow ("High band 0-1 (set by SolarSystem)", Float) = 0
        _Clock ("Time (set by SolarSystem)", Float) = 0
        _Alpha ("Fade (set by SolarSystem)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+2" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _Glint;
                float _Size;
                float _Orbit;
                float _Omega;
                float _Reference;
                float _Glow;
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

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float radius = max(input.positionOS.x, 1e-3);
                float height = input.positionOS.y;
                float angle = input.positionOS.z + _Orbit * _Omega * pow(_Reference / radius, 1.5);
                float3 local = float3(cos(angle) * radius, height, sin(angle) * radius);
                float3 positionWS = TransformObjectToWorld(local);
                float scale = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));

                float seed = input.seed.x;
                float glint = pow(saturate(sin(_Clock * (0.9 + seed * 2.3) + seed * 61.0) * 0.5 + 0.5), 10) * (0.6 + _Glow * 1.2);
                float size = _Size * scale * input.seed.y * (1 + glint * 0.9);
                float3 positionVS = TransformWorldToView(positionWS);
                positionVS.xy += (input.uv * 2 - 1) * size;
                output.positionCS = TransformWViewToHClip(positionVS);
                output.uv = input.uv;

                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 outward = positionWS - originWS;
                float3 toEye = GetCameraPositionWS() - originWS;
                float near = dot(outward, toEye) * rsqrt(max(dot(outward, outward) * dot(toEye, toEye), 1e-10)) * 0.5 + 0.5;
                float3 color = lerp(_Color.rgb * (0.75 + seed * 0.4), _Glint.rgb, saturate(glint));
                output.color = float4(color, lerp(0.35, 1.0, near) * (0.45 + glint * 1.2 + _Glow * 0.2) * _Alpha);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float d = length(input.uv * 2 - 1);
                float core = saturate(1 - d);
                float3 color = lerp(input.color.rgb, float3(1, 1, 1), pow(core, 6) * 0.5);
                return half4(color, saturate(core * core * 1.4 * input.color.a));
            }
            ENDHLSL
        }
    }
}
