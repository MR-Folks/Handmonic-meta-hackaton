// The Heartbeat orb: a glowing ball of the song. Colour bands swirl through it, the surface ripples with the
// bass, the rim burns brighter and the core flashes white on each kick.
Shader "MusicInYourHands/Orb"
{
    Properties
    {
        _ColorA ("Swirl A", Color) = (1, 0.37, 0.68, 1)
        _ColorB ("Swirl B", Color) = (0.62, 0.56, 1, 1)
        _ColorC ("Swirl C", Color) = (0.5, 0.72, 1, 1)
        _RimColor ("Rim", Color) = (1, 0.83, 0.42, 1)
        _Kick ("Beat 0-1 (set by HeartbeatOrb)", Float) = 0
        _Bass ("Bass 0-1 (set by HeartbeatOrb)", Float) = 0
        _Highs ("Highs 0-1 (set by HeartbeatOrb)", Float) = 0
        _Alpha ("Alpha (set by HeartbeatOrb)", Float) = 1
        _Ripple ("Bass ripple", Float) = 0.025
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+2" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _ColorA;
                float4 _ColorB;
                float4 _ColorC;
                float4 _RimColor;
                float _Kick;
                float _Bass;
                float _Highs;
                float _Alpha;
                float _Ripple;
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
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float t = _Time.y;
                float3 p = input.positionOS.xyz;
                // Slow, layered ripples that swell with the bass.
                float ripple = sin(p.y * 14 + t * 2.2) * 0.5 + sin((p.x + p.z) * 11 - t * 1.7) * 0.5;
                p += input.normalOS * ripple * _Ripple * (0.25 + _Bass);
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(p);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _Time.y;
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float ndv = saturate(dot(n, v));
                float fresnel = pow(1 - ndv, 2.2);
                float core = pow(ndv, 3);

                float3 p = input.positionOS * 2;
                float swirlA = sin(p.y * 6 + sin(p.x * 5 + t * 1.3) * 1.6 + t * 1.8) * 0.5 + 0.5;
                float swirlB = sin(p.x * 4 - p.z * 5 + t * 0.9) * 0.5 + 0.5;
                float3 color = lerp(_ColorC.rgb, _ColorB.rgb, swirlA);
                color = lerp(color, _ColorA.rgb, swirlB * 0.6);
                color = lerp(color, _RimColor.rgb, fresnel * (0.55 + _Kick * 0.45));
                // The kick brightens the colours from inside rather than washing them out to white.
                color *= 1 + core * (_Kick * 0.6 + _Highs * 0.15);
                color = lerp(color, 1, core * _Kick * 0.25);

                float alpha = 0.22 + fresnel * 0.75 + core * (0.1 + _Kick * 0.25);
                return half4(color, saturate(alpha) * _Alpha);
            }
            ENDHLSL
        }
    }
}
