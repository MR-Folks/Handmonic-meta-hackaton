// Water light: the shifting bright web that light through rippling water throws on the floor below it. Drawn
// additively on a flat quad under the Sound pool; brighter with the bass and when the water is stirred. _Sharpness,
// _Fill and _Drift match the room's water light (RoomRipples); their defaults are the original look.
Shader "MusicInYourHands/Caustics"
{
    Properties
    {
        _Color ("Colour", Color) = (0.45, 0.85, 1, 1)
        _Strength ("Strength (set by SoundPool)", Float) = 0.5
        _Clock ("Time (set by SoundPool)", Float) = 0
        _Scale ("Pattern scale (per metre)", Float) = 7
        _Sharpness ("How thin and bright the web's lines are", Float) = 12
        _Fill ("Soft even light between the lines, 0-1", Float) = 0
        _Drift ("How fast the web shifts", Float) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-8" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float _Strength;
                float _Clock;
                float _Scale;
                float _Sharpness;
                float _Fill;
                float _Drift;
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

            // Two warped interference grids: where either crosses zero, light gathers into a thin bright line. The
            // warps drift, so the web of lines slowly bends, breaks and rejoins like light under moving water.
            float Caustic(float2 p, float t, float sharpness)
            {
                float2 a = p + float2(sin(p.y * 1.7 + t * 0.9), cos(p.x * 1.3 - t * 0.7)) * 0.65;
                float2 b = p * 1.37 + float2(cos(p.y * 1.1 - t * 0.6), sin(p.x * 1.9 + t * 0.8)) * 0.65;
                float l1 = abs(sin(a.x * 2.0) * sin(a.y * 2.0));
                float l2 = abs(sin(b.x * 2.0 + 1.3) * sin(b.y * 2.0 + 0.7));
                return pow(1.0 - l1, sharpness) * 0.7 + pow(1.0 - l2, sharpness) * 0.55;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                // Laid out in world space (_Scale per metre), so the room's water light (RoomRipples) carries the same
                // web on past this patch without a seam.
                float light = saturate(Caustic(input.positionWS.xz * _Scale, _Clock * _Drift, _Sharpness));
                light = lerp(light, 0.3, _Fill);
                // Fade out softly towards the edges of the patch of light.
                float2 e = min(uv, 1 - uv);
                float edge = smoothstep(0.0, 0.22, min(e.x, e.y));
                float alpha = light * edge * _Strength * 0.7;
                return half4(lerp(_Color.rgb, 1, light * 0.35), saturate(alpha));
            }
            ENDHLSL
        }
    }
}
