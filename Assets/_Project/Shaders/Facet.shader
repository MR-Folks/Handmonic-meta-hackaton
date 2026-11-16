// Low-poly beads: solid shapes with flat facets (each face has its own normal in the mesh), lit softly from above with
// a glint where a facet catches the light and a glow at the silhouette. Drawn instanced, one draw call for all of them;
// each bead's colour comes from _Tint (rgb), and its alpha makes the bead glow brighter. Opaque, so beads under the
// water show through its tint like things in it. _Shade, _Glint, _GlintPower and _Rim tune how lively the facets are;
// their defaults are the original look, and a world wanting calmer beads sets its own on a copy of the material.
Shader "MusicInYourHands/Facet"
{
    Properties
    {
        _Light ("Light direction", Vector) = (0.3, 1, -0.25, 0)
        _Shade ("How much the facets differ in light, 0-1", Float) = 0.65
        _Glint ("Glint where a facet catches the light", Float) = 0.7
        _GlintPower ("Glint sharpness (higher is smaller)", Float) = 24
        _Rim ("Glow at the silhouette", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Light;
                float _Shade;
                float _Glint;
                float _GlintPower;
                float _Rim;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Beads)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
            UNITY_INSTANCING_BUFFER_END(Beads)

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Beads, _Tint);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 l = normalize(_Light.xyz);
                float lit = saturate(dot(n, l)) * _Shade + (1 - _Shade);
                float glint = pow(saturate(dot(reflect(-l, n), v)), _GlintPower) * _Glint;
                float rim = pow(1 - saturate(abs(dot(n, v))), 2) * _Rim;
                float3 color = tint.rgb * (lit + rim) * (1 + tint.a) + glint;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
