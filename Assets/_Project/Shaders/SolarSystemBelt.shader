// The Solar system's asteroid belt: hundreds of small glints circling you just beyond Mars, all in one mesh. Each
// asteroid is a quad whose four corners hold its orbit (radius, height above the plane, starting angle) in the position
// and its seed and size in uv2. The vertex shader moves it round at Kepler's speed for its distance (_Orbit is the
// system's orbit clock, _Omega the angular speed at the _Reference distance) and turns it to face the viewer. Now and
// then an asteroid glints, more often and brighter with the high band (_Glow), and the beat's wave of light from the
// sun (_SunPos, _Wave) lights them up as it passes. Like the orbit lines, the belt dips under your face if you lean
// in and fades into a scanned wall (SolarSystemRoom.hlsl).
Shader "MusicInYourHands/SolarSystemBelt"
{
    Properties
    {
        _Color ("Rock colour", Color) = (0.86, 0.78, 0.66, 1)
        _Glint ("Glint colour", Color) = (1, 0.95, 0.82, 1)
        _Size ("Asteroid size, m (set by SolarSystem)", Float) = 0.003
        _Orbit ("Orbit clock (set by SolarSystem)", Float) = 0
        _Omega ("Angular speed at the reference distance (set by SolarSystem)", Float) = 0.22
        _Reference ("Reference distance (set by SolarSystem)", Float) = 0.61
        _Glow ("High band 0-1 (set by SolarSystem)", Float) = 0
        _Clock ("Time (set by SolarSystem)", Float) = 0
        _Alpha ("Fade (set by SolarSystem)", Float) = 1
        _SunPos ("Sun position (set by SolarSystem)", Vector) = (0, 0, 0, 0)
        _Wave ("Beat wave: seconds since, strength, speed, width (set by SolarSystem)", Vector) = (9, 0, 3.5, 0.3)
        _EnvironmentDepthBias ("Real-room depth bias (share of the distance)", Float) = 0.03
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
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SolarSystemRoom.hlsl"
            #include "RoomOcclusion.hlsl"

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
                float4 _SunPos;
                float4 _Wave;
                float _EnvironmentDepthBias;
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
                META_DEPTH_VERTEX_OUTPUT(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float radius = max(input.positionOS.x, 1e-3);
                float height = input.positionOS.y;
                float angle = input.positionOS.z + _Orbit * _Omega * pow(abs(_Reference / radius), 1.5);
                float3 local = float3(cos(angle) * radius, height, sin(angle) * radius);
                float3 positionWS = SolarClear(TransformObjectToWorld(local));
                float scale = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));

                float seed = input.seed.x;
                float wave = SolarWave(distance(positionWS, _SunPos.xyz), _Wave);
                float glint = pow(saturate(sin(_Clock * (0.9 + seed * 2.3) + seed * 61.0) * 0.5 + 0.5), 10) * (0.6 + _Glow * 1.2);
                float size = _Size * scale * input.seed.y * (1 + glint * 0.9 + wave * 0.8);
                float3 positionVS = TransformWorldToView(positionWS);
                positionVS.xy += (input.uv * 2 - 1) * size;
                output.positionCS = TransformWViewToHClip(positionVS);
                // Tested against the room at its centre: an asteroid is only a few millimetres across.
                ROOM_OCCLUSION_WORLD(output, positionWS);
                output.uv = input.uv;

                // Dimmer far off, gone right by your eyes, faded into the walls.
                float dist = distance(GetCameraPositionWS(), positionWS);
                float shade = lerp(1.0, 0.6, saturate((dist - 1.2) / 2.5)) * smoothstep(0.12, 0.3, dist) * SolarInRoom(positionWS);
                float3 color = lerp(_Color.rgb * (0.75 + seed * 0.4), _Glint.rgb, saturate(glint + wave));
                output.color = float4(color, (0.4 + glint * 1.1 + _Glow * 0.2 + wave * 1.5) * shade * _Alpha);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float d = length(input.uv * 2 - 1);
                float core = saturate(1 - d);
                float3 color = lerp(input.color.rgb, float3(1, 1, 1), pow(core, 6) * 0.5);
                float visible = META_DEPTH_GET_OCCLUSION_VALUE(input, _EnvironmentDepthBias);
                return half4(color, saturate(core * core * 1.4 * input.color.a) * visible);
            }
            ENDHLSL
        }
    }
}
