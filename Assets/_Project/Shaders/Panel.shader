// Flat glass for the menu's pages: a rounded rectangle with a fine edge line, sharp at any size because its shape is
// worked out in metres. _Size is the quad's size in metres; _Hover lightens it under a pointer. _Soft feathers the edge
// (metres) for a soft backing behind a caption. With _Picture at 1 it shows _MainTex (an emoji on the song maker's page)
// over its fill.
Shader "MusicInYourHands/Panel"
{
    Properties
    {
        _Color ("Fill", Color) = (0.05, 0.055, 0.09, 0.86)
        _Edge ("Edge line", Color) = (1, 1, 1, 0.18)
        _Size ("Size (m)", Vector) = (1, 1, 0, 0)
        _Radius ("Corner radius (m)", Float) = 0.015
        _Hover ("Hover 0-1", Float) = 0
        _Alpha ("Fade", Float) = 1
        _Soft ("Soft edge (m)", Float) = 0
        _MainTex ("Picture", 2D) = "white" {}
        _Picture ("Picture 0-1", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+40" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
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
                float4 _Edge;
                float4 _Size;
                float _Radius;
                float _Hover;
                float _Alpha;
                float _Soft;
                float _Picture;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 half_size = _Size.xy * 0.5;
                float2 corner = abs((input.uv - 0.5) * _Size.xy) - (half_size - _Radius);
                float distance = length(max(corner, 0)) + min(max(corner.x, corner.y), 0) - _Radius;
                float soft = max(fwidth(distance), 1e-5);
                float feather = max(soft, _Soft);
                float inside = smoothstep(feather, -feather, distance);
                float line_ = saturate(1 - abs(distance + 0.0007) / (0.0007 + soft));

                float3 color = lerp(_Color.rgb, 1, _Hover * 0.16);
                float alpha = saturate(_Color.a + _Hover * 0.14);
                color = lerp(color, _Edge.rgb, line_ * _Edge.a);
                alpha = max(alpha, line_ * _Edge.a);
                half4 picture = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Picture;
                color = lerp(color, picture.rgb, picture.a);
                alpha = max(alpha, picture.a);
                return half4(color, alpha * inside * _Alpha);
            }
            ENDHLSL
        }
    }
}
