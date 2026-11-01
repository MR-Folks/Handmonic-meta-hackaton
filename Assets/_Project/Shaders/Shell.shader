// A 3D wave on a sphere that grows out of the palm on each beat: bright at its silhouette, so it reads as a round shell
// rather than a flat disc, with rings of light around _Axis (the way the palm faces) that travel outwards. It is drawn
// on both sides for depth, fades towards the back of the hand, and fades away before it reaches the eyes.
Shader "MusicInYourHands/Shell"
{
    Properties
    {
        _Color ("Color", Color) = (0.62, 0.56, 1, 1)
        _Alpha ("Alpha", Float) = 1
        _Axis ("Way the palm faces, world space (set by LivingSkin)", Vector) = (0, 0, 1, 0)
        _Phase ("Rings' travel (set by LivingSkin)", Float) = 0
        _Rings ("Rings", Float) = 7
        _RimPower ("Rim sharpness", Float) = 2.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _Axis;
                float _Phase;
                float _Rings;
                float _RimPower;
            CBUFFER_END

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
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1 - abs(dot(n, v)), _RimPower);

                // 1 straight ahead of the palm, 0 to the side, -1 behind the hand.
                float ahead = dot(n, _Axis.xyz);
                float rings = pow(saturate(sin(ahead * _Rings * PI + _Phase) * 0.5 + 0.5), 6);
                float front = smoothstep(-0.45, 0.3, ahead);

                // A shell that reaches the eyes would fill the whole view, so it is gone before it gets there.
                float near = saturate((distance(input.positionWS, GetCameraPositionWS()) - 0.08) / 0.14);

                float a = saturate((rim * 0.9 + rings * 0.3 + 0.025) * front * near * _Alpha * _Color.a);
                return half4(lerp(_Color.rgb, 1, rim * 0.3), a);
            }
            ENDHLSL
        }
    }
}
