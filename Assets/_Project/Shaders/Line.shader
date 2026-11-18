// A glowing line: a bright thin core with a soft glow across its width. Drawn on camera-facing strips built by
// LineStrip (ribbons, strings, trails). Vertex colour sets the colour and fades the ends. A world's own copy with
// _RoomOcclusion 1 hides behind real things while the real room's depth is on (RoomOcclusion.hlsl).
Shader "MusicInYourHands/Line"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _Core ("White-hot core", Range(0, 1)) = 0.4
        // How the colour meets what is behind: 1 (One) adds light, the glow every line has; 10 (OneMinusSrcAlpha)
        // lays the colour over, so a line still shows against something bright, such as Lightning's snow.
        [HideInInspector] _DstBlend ("Colour blend", Float) = 1
        _RoomOcclusion ("Hidden by the real room 0-1 (a world's own copy)", Float) = 0
        _EnvironmentDepthBias ("Real-room depth bias (share of the distance)", Float) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+8" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha [_DstBlend], One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RoomOcclusion.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Core;
                float _RoomOcclusion;
                float _EnvironmentDepthBias;
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
                META_DEPTH_VERTEX_OUTPUT(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                META_DEPTH_INITIALIZE_VERTEX_OUTPUT(output, input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float across = abs(input.uv.y * 2 - 1);
                float glow = exp(-across * across * 5);
                float core = exp(-across * across * 70);
                float3 color = lerp(_Color.rgb * input.color.rgb, 1, core * _Core);
                float alpha = saturate((glow * 0.75 + core) * input.color.a * _Color.a * _Intensity);
                float visible = META_DEPTH_GET_OCCLUSION_VALUE(input, _EnvironmentDepthBias);
                alpha *= lerp(1, visible, _RoomOcclusion);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
