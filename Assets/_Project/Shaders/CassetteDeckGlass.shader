// Clear smoky plastic for the cassette deck: a cassette's window and the radio's cassette door. Mostly see-through,
// more opaque and brighter at grazing angles, with a soft diagonal reflection streak so it reads as glass. It blends
// its alpha over what is behind it, so over the deck's solid parts the picture stays opaque for passthrough.
// Cut below _CassetteDeckClip like the other deck parts.
Shader "MusicInYourHands/CassetteDeckGlass"
{
    Properties
    {
        _Color ("Tint and opacity", Color) = (0.55, 0.6, 0.7, 0.22)
        _Reflection ("Reflection", Range(0, 1)) = 0.35
        _Glow ("Glow 0-1 (set per part)", Float) = 0
        _GlowColor ("Glow colour (set per part)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Reflection;
                float _Glow;
                float4 _GlowColor;
            CBUFFER_END

            float4 _CassetteDeckClip;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_CassetteDeckClip.y > 0.5) clip(input.positionWS.y - _CassetteDeckClip.x);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1 - saturate(abs(dot(n, v))), 3);
                // A soft streak across the glass, fixed to the world so it slides as you move your head.
                float streak = smoothstep(0.06, 0.0, abs(frac((input.positionWS.x + input.positionWS.y) * 9) - 0.5) - 0.38);
                float3 color = _Color.rgb + (fresnel * 0.6 + streak * _Reflection) * float3(0.9, 0.95, 1);
                color += _GlowColor.rgb * _Glow * 0.8;
                float alpha = saturate(_Color.a + fresnel * 0.5 + streak * _Reflection * 0.5 + _Glow * 0.25);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
